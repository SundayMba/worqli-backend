using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Notifications;
using Servika.Domain.Bookings;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Reverses a booking's escrow when it's resolved in the customer's favour. Mirrors
/// the payment split but backwards: the customer is credited a
/// <see cref="WalletTransactionType.Refund"/> for the full amount, and the platform
/// commission + artisan earning are clawed back with offsetting
/// <see cref="WalletTransactionType.Adjustment"/> entries — so every booking-scoped
/// balance nets to zero and the "refunds" total is exactly what customers got back.
///
/// <para>Stages the changes only (no <c>SaveChanges</c>), so it commits inside the
/// caller's transaction. A no-op when the booking was never paid, and idempotent —
/// only a <see cref="PaymentStatus.Succeeded"/> payment can be refunded, once.</para>
/// </summary>
public sealed class RefundService
{
    private readonly IPaymentRepository _payments;
    private readonly IWalletRepository _wallet;
    private readonly NotificationEmitter _notifications;
    private readonly IPaymentGateway _gateway;
    private readonly IClock _clock;

    public RefundService(
        IPaymentRepository payments,
        IWalletRepository wallet,
        NotificationEmitter notifications,
        IPaymentGateway gateway,
        IClock clock)
    {
        _payments = payments;
        _wallet = wallet;
        _notifications = notifications;
        _gateway = gateway;
        _clock = clock;
    }

    /// <summary>Refunds the booking's settled payment if there is one. Returns the
    /// refunded amount (0 if nothing was paid).</summary>
    public async Task<int> RefundIfPaidAsync(Booking booking, CancellationToken ct)
    {
        var payment = await _payments.FindSucceededForBookingAsync(booking.Id, ct);
        if (payment is null) return 0; // never paid — nothing to return

        var now = _clock.UtcNow;

        // Money back to the customer (the "refund" the KPI counts).
        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Customer, payment.CustomerId,
            WalletTransactionType.Refund, payment.AmountNaira,
            booking.Id, payment.Id, $"Refund for booking {booking.Id}", now));

        // Only claw back the split that was actually released. Before completion
        // the escrow is still held (no commission/earning entries exist), so there
        // is nothing to reverse — clawing back then would push balances negative.
        if (payment.IsEarningReleased)
        {
            if (payment.CommissionNaira > 0)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                    WalletTransactionType.Adjustment, -payment.CommissionNaira,
                    booking.Id, payment.Id, $"Commission reversal for the refund on booking {booking.Id}", now));

            if (payment.ArtisanId is { } artisanId && payment.ArtisanEarningNaira > 0)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Artisan, artisanId,
                    WalletTransactionType.Adjustment, -payment.ArtisanEarningNaira,
                    booking.Id, payment.Id, $"Earning reversal for the refund on booking {booking.Id}", now));
        }

        payment.MarkRefunded(now);
        booking.MarkRefunded();
        _notifications.RefundIssued(booking, payment.AmountNaira);

        // Send the real money back to the customer's card/bank. Best-effort: the
        // ledger already records the refund, so a provider hiccup is logged by the
        // gateway (Infrastructure) for ops to retry rather than aborting the dispute
        // resolution. The stub just logs. Paystack refunds settle asynchronously; the
        // request being accepted is what matters here.
        await _gateway.RefundAsync(payment.Reference, payment.AmountNaira, ct);

        return payment.AmountNaira;
    }
}
