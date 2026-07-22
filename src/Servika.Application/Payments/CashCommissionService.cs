using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Domain.Bookings;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Records Servika's commission on a CASH job at completion. Online payments
/// deduct the commission automatically when the escrow settles; cash never
/// touches the platform, so the artisan's ledger is debited instead —
/// <see cref="WalletTransactionType.CommissionDue"/>. The debt auto-nets
/// against future online earnings (a balance is just the ledger sum), or the
/// artisan settles it explicitly. A no-op while the launch commission is 0%.
/// </summary>
public sealed class CashCommissionService
{
    private readonly IWalletRepository _wallet;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    public CashCommissionService(
        IWalletRepository wallet,
        Notifications.NotificationEmitter notifications,
        IClock clock)
    {
        _wallet = wallet;
        _notifications = notifications;
        _clock = clock;
    }

    /// <summary>Debits the artisan's ledger for a completed cash job's commission.
    /// Stages onto the shared DbContext — the caller's SaveChanges commits it in
    /// the same transaction as the completion itself.</summary>
    public async Task RecordIfCashJobAsync(Booking booking, CancellationToken ct)
    {
        // Escrow-paid jobs already had their commission taken at settlement.
        if (booking.PaymentState == BookingPaymentState.Paid) return;
        if (booking.PaymentMethod != PaymentMethod.Cash) return;
        if (booking.ArtisanId is not { } artisanId) return;
        if (booking.InitialQuoteAmountNaira is not { } amount || amount <= 0) return;

        var commission = (int)Math.Round(
            amount * booking.CommissionRate, MidpointRounding.AwayFromZero);
        if (commission <= 0) return; // launch window (0%) — nothing to record

        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Artisan, artisanId,
            WalletTransactionType.CommissionDue, -commission,
            booking.Id, null,
            $"Service fee on cash booking {booking.Id}", _clock.UtcNow));

        await _notifications.ArtisanCommissionRecorded(booking, commission, ct);
    }
}
