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
    private readonly BankAccountResolver _resolver;
    /// <summary>Default smallest payout we'll process (artisan earnings).</summary>
    public const int DefaultMinWithdrawalNaira = 1000;

    private readonly IWalletRepository _wallet;
    private readonly IWithdrawalRepository _withdrawals;
    private readonly IPayoutGateway _payouts;
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public WithdrawalService(
        IWalletRepository wallet,
        IWithdrawalRepository withdrawals,
        IPayoutGateway payouts,
        IPlatformSettingsRepository settings,
        IClock clock,
        BankAccountResolver resolver)
    {
        _resolver = resolver;
        _wallet = wallet;
        _withdrawals = withdrawals;
        _payouts = payouts;
        _settings = settings;
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

        // With a bank code we can ask the bank whose account it is; the stored and paid
        // name is then the bank's, not the typed one.
        var accountName = request.AccountName;
        if (!string.IsNullOrWhiteSpace(request.BankCode))
        {
            var resolved = await _resolver.ResolveAsync(userId, request.BankCode, request.AccountNumber, request.AccountName, ct)
                ?? throw new ArgumentException($"No account with that number at {request.BankName}. Check the digits.");
            accountName = resolved.AccountName;
        }

        var now = _clock.UtcNow;

        // The bank-transfer charge. Servika pays it during the launch window; after
        // FeesStartAtUtc it comes out of the amount, so the owner receives amount − fee.
        // Either way the true gateway cost is booked on the platform ledger.
        var settings = await _settings.GetOrCreateAsync(ct);
        var fee = FeePolicy.TransferFee(request.AmountNaira, settings);
        var bearer = FeePolicy.UsersBearFees(settings, now) ? FeeBearer.User : FeeBearer.Platform;
        if (bearer == FeeBearer.User && request.AmountNaira - fee < 100)
            throw new ConflictException($"After the ₦{fee:N0} transfer charge too little would reach your bank. Withdraw at least ₦{Math.Max(minNaira, fee + 100):N0}.");

        var withdrawal = Withdrawal.Request(
            ownerType, ownerId, userId, request.AmountNaira,
            request.BankName, request.AccountNumber, accountName, now, fee, bearer);
        _withdrawals.Add(withdrawal);

        // Reserve the funds immediately with an append-only ledger debit.
        _wallet.Add(WalletTransaction.Create(
            ownerType, ownerId, WalletTransactionType.PayoutRequest,
            -request.AmountNaira, null, null,
            bearer == FeeBearer.User && fee > 0
                ? $"Payout to {withdrawal.BankName} {withdrawal.AccountNumberMasked} (₦{withdrawal.NetNaira:N0} after ₦{fee:N0} transfer charge)"
                : $"Payout to {withdrawal.BankName} {withdrawal.AccountNumberMasked}", now));
        RecordTransferFee(_wallet, withdrawal, reverse: false, now);

        // Disburse the NET amount. The stub succeeds synchronously; Paystack Transfers
        // accepts the transfer and returns Pending — the real result then arrives on
        // the transfer webhook (HandleTransferWebhookHandler), which finalises the ledger.
        var result = await _payouts.DisburseAsync(
            new PayoutInput(withdrawal.Id.ToString(), withdrawal.NetNaira,
                request.BankName, request.BankCode, request.AccountNumber, accountName), ct);

        switch (result.Outcome)
        {
            case PayoutOutcome.Succeeded:
                withdrawal.MarkPaid(_payouts.Provider, result.Reference, now);
                break;

            case PayoutOutcome.Pending:
                // Transfer accepted, not yet settled. Keep the reservation and the
                // Pending status; stamp the provider + transfer code for the webhook.
                withdrawal.BeginProcessing(_payouts.Provider, result.Reference);
                break;

            default: // Failed
                withdrawal.MarkFailed(_payouts.Provider, result.FailureReason, now);
                // Reverse the reservation so the balance is made whole.
                _wallet.Add(WalletTransaction.Create(
                    ownerType, ownerId, WalletTransactionType.Adjustment,
                    request.AmountNaira, null, null,
                    $"Reversal for failed payout {withdrawal.Id}", now));
                RecordTransferFee(_wallet, withdrawal, reverse: true, now);
                break;
        }

        await _withdrawals.SaveChangesAsync(ct);
        return withdrawal.ToDto();
    }

    /// <summary>
    /// Books the transfer charge on the platform ledger: the gateway's cost always
    /// (<see cref="WalletTransactionType.GatewayCost"/>), and, when the owner bore it,
    /// the matching <see cref="WalletTransactionType.TransferFee"/> income so Servika
    /// nets to zero. <paramref name="reverse"/> writes the negated pair when a transfer
    /// fails (the gateway does not charge for a failed transfer).
    /// </summary>
    public static void RecordTransferFee(IWalletRepository wallet, Withdrawal w, bool reverse, DateTimeOffset now)
    {
        if (w.FeeNaira <= 0) return;
        var sign = reverse ? -1 : 1;
        var suffix = reverse ? $" (reversed, payout {w.Id} failed)" : "";
        wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
            WalletTransactionType.GatewayCost, -w.FeeNaira * sign, null, null,
            $"Transfer charge on payout {w.Id}{suffix}", now));
        if (w.FeeBearer == FeeBearer.User)
            wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.TransferFee, w.FeeNaira * sign, null, null,
                $"Transfer charge collected on payout {w.Id}{suffix}", now));
    }
}
