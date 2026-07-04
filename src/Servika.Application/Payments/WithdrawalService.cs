using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Owner-agnostic payout engine shared by every cash-out (artisan earnings,
/// referral rewards). Validates the amount against the owner's <b>ledger-computed</b>
/// balance (never trusted from the client), reserves the funds with a
/// <see cref="WalletTransactionType.PayoutRequest"/> debit, disburses via the payout
/// gateway, and reverses the reservation with an offsetting credit if the
/// disbursement fails — so the append-only ledger always nets to the true balance.
/// </summary>
public sealed class WithdrawalService
{
    /// <summary>Default smallest payout we'll process (artisan earnings).</summary>
    public const int DefaultMinWithdrawalNaira = 1000;

    private readonly IWalletRepository _wallet;
    private readonly IWithdrawalRepository _withdrawals;
    private readonly IPayoutGateway _payouts;
    private readonly IClock _clock;

    public WithdrawalService(
        IWalletRepository wallet,
        IWithdrawalRepository withdrawals,
        IPayoutGateway payouts,
        IClock clock)
    {
        _wallet = wallet;
        _withdrawals = withdrawals;
        _payouts = payouts;
        _clock = clock;
    }

    /// <summary>
    /// Cashes out from the <paramref name="ownerType"/>/<paramref name="ownerId"/>
    /// wallet to a bank account, requested by <paramref name="userId"/>. Enforces a
    /// <paramref name="minNaira"/> floor and the available balance; commits the
    /// withdrawal + its ledger entries in one <c>SaveChanges</c>.
    /// </summary>
    public async Task<WithdrawalDto> WithdrawAsync(
        WalletOwnerType ownerType,
        Guid ownerId,
        Guid userId,
        RequestWithdrawalRequest request,
        int minNaira,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BankName) ||
            string.IsNullOrWhiteSpace(request.AccountNumber) ||
            string.IsNullOrWhiteSpace(request.AccountName))
        {
            throw new ArgumentException("Bank name, account number and account name are required.");
        }

        if (request.AmountNaira < minNaira)
            throw new ConflictException($"Minimum withdrawal is ₦{minNaira:N0}.");

        var available = await _wallet.GetBalanceAsync(ownerType, ownerId, ct);
        if (request.AmountNaira > available)
            throw new ConflictException($"Insufficient balance. Available: ₦{available:N0}.");

        var now = _clock.UtcNow;
        var withdrawal = Withdrawal.Request(
            ownerType, ownerId, userId, request.AmountNaira,
            request.BankName, request.AccountNumber, request.AccountName, now);
        _withdrawals.Add(withdrawal);

        // Reserve the funds immediately with an append-only ledger debit.
        _wallet.Add(WalletTransaction.Create(
            ownerType, ownerId, WalletTransactionType.PayoutRequest,
            -request.AmountNaira, null, null,
            $"Payout to {withdrawal.BankName} {withdrawal.AccountNumberMasked}", now));

        // Disburse. The stub succeeds synchronously; a real provider's async result
        // would arrive via a transfer webhook (a later step) instead.
        var result = await _payouts.DisburseAsync(
            new PayoutInput(withdrawal.Id.ToString(), request.AmountNaira,
                request.BankName, request.AccountNumber, request.AccountName), ct);

        if (result.Outcome == PayoutOutcome.Succeeded)
        {
            withdrawal.MarkPaid(_payouts.Provider, result.Reference, now);
        }
        else
        {
            withdrawal.MarkFailed(_payouts.Provider, result.FailureReason, now);
            // Reverse the reservation so the balance is made whole.
            _wallet.Add(WalletTransaction.Create(
                ownerType, ownerId, WalletTransactionType.Adjustment,
                request.AmountNaira, null, null,
                $"Reversal — payout {withdrawal.Id} failed", now));
        }

        await _withdrawals.SaveChangesAsync(ct);
        return withdrawal.ToDto();
    }
}
