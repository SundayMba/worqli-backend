using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Applies a payout provider's transfer webhook (Paystack <c>transfer.success</c> /
/// <c>transfer.failed</c> / <c>transfer.reversed</c>). The signature is verified
/// first (a forged body is a 401), then the transfer is matched to our Pending
/// withdrawal by reference (= the withdrawal id). It is <b>idempotent</b>: an
/// unknown reference or an already-settled withdrawal is a no-op, so the provider
/// can safely retry. On success the payout is marked Paid; on failure/reversal it's
/// marked Failed and the reserved funds are returned to the ledger.
/// </summary>
public sealed class HandleTransferWebhookHandler
{
    private readonly IWithdrawalRepository _withdrawals;
    private readonly IWalletRepository _wallet;
    private readonly IPayoutGateway _gateway;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public HandleTransferWebhookHandler(
        IWithdrawalRepository withdrawals,
        IWalletRepository wallet,
        IPayoutGateway gateway,
        NotificationEmitter notifications,
        IClock clock)
    {
        _withdrawals = withdrawals;
        _wallet = wallet;
        _gateway = gateway;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task HandleAsync(string rawBody, string? signature, CancellationToken ct)
    {
        if (!_gateway.VerifySignature(rawBody, signature))
            throw new InvalidWebhookSignatureException();

        var evt = _gateway.ParseTransferWebhook(rawBody);
        if (evt is null)
            return; // not a transfer event we care about

        if (!Guid.TryParse(evt.Reference, out var withdrawalId))
            return; // reference isn't one of ours

        var withdrawal = await _withdrawals.FindByIdAsync(withdrawalId, ct);
        if (withdrawal is null || withdrawal.Status != WithdrawalStatus.Pending)
            return; // unknown or already settled — idempotent no-op

        var now = _clock.UtcNow;

        if (evt.Outcome == PayoutOutcome.Succeeded)
        {
            withdrawal.MarkPaid(_gateway.Provider, null, now);
            _notifications.PayoutSent(
                withdrawal.UserId, withdrawal.AmountNaira, withdrawal.AccountNumberMasked);
        }
        else
        {
            withdrawal.MarkFailed(_gateway.Provider, "Transfer failed at the bank.", now);
            // Return the reserved funds to the owner's ledger.
            _wallet.Add(WalletTransaction.Create(
                withdrawal.OwnerType, withdrawal.OwnerId, WalletTransactionType.Adjustment,
                withdrawal.AmountNaira, null, null,
                $"Reversal — payout {withdrawal.Id} failed", now));
            _notifications.PayoutFailed(withdrawal.UserId, withdrawal.AmountNaira);
        }

        await _withdrawals.SaveChangesAsync(ct);
    }
}
