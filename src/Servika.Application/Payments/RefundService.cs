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

    /// <summary>Fully refunds the booking's settled payment if there is one. Returns
    /// the refunded amount (0 if nothing was paid).</summary>
    public async Task<int> RefundIfPaidAsync(Booking booking, CancellationToken ct)
    {
        var payment = await _payments.FindSucceededForBookingAsync(booking.Id, ct);
        if (payment is null || payment.IsRefundRequested) return 0; // never paid / already refunded

        // A materials advance the customer explicitly released is theirs no longer:
        // the artisan bought the parts with it. Everything else comes back.
        var advance = booking.ReleasedMaterialsAdvanceNaira;
        if (advance > 0)
            return await PartialRefundIfPaidAsync(booking, payment.AmountNaira - advance, ct, refundAll: true);

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
        booking.MarkRefunded(payment.AmountNaira);
        _notifications.RefundIssued(booking, payment.AmountNaira);
        await _gateway.RefundAsync(payment.Reference, payment.AmountNaira, ct);

        return payment.AmountNaira;
    }

    /// <summary>Partially refunds a paid booking: the customer gets
    /// <paramref name="refundNaira"/> back and the artisan keeps the rest as their
    /// earning for work rendered. Used when an admin resolves a dispute with a partial
    /// refund. Falls back to a full refund if the amount covers (or exceeds) the whole
    /// payment. Returns the amount refunded to the customer (0 if nothing was paid).
    ///
    /// <para>The remainder is settled to the artisan here (released from escrow if it
    /// was still held, or left in place / clawed down if it had already been released
    /// at an earlier completion), so every booking-scoped balance nets correctly.</para>
    /// </summary>
    public Task<int> PartialRefundIfPaidAsync(Booking booking, int refundNaira, CancellationToken ct) =>
        PartialRefundIfPaidAsync(booking, refundNaira, ct, refundAll: false);

    private async Task<int> PartialRefundIfPaidAsync(
        Booking booking, int refundNaira, CancellationToken ct, bool refundAll)
    {
        var payment = await _payments.FindSucceededForBookingAsync(booking.Id, ct);
        if (payment is null || payment.IsRefundRequested) return 0;

        var full = payment.AmountNaira;
        // A released materials advance can never be refunded (see RefundIfPaidAsync).
        var advance = booking.ReleasedMaterialsAdvanceNaira;
        refundNaira = Math.Min(refundNaira, full - advance);
        if (!refundAll && refundNaira >= full) return await RefundIfPaidAsync(booking, ct); // whole thing
        if (refundNaira <= 0) return 0;

        var now = _clock.UtcNow;
        var keep = full - refundNaira;                 // the artisan's portion (incl. any advance)
        var rate = payment.CommissionRate;
        var commissionKeep = (int)Math.Round(keep * rate, MidpointRounding.AwayFromZero);
        var earningKeep = keep - commissionKeep;

        // Money back to the customer (the refunded portion).
        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Customer, payment.CustomerId,
            WalletTransactionType.Refund, refundNaira,
            booking.Id, payment.Id, $"Partial refund for booking {booking.Id}", now));

        if (payment.IsEarningReleased)
        {
            // The full split is already in the ledger; claw back only the refunded
            // portion so the artisan is left holding earningKeep and the platform
            // commissionKeep.
            var artisanClawback = payment.ArtisanEarningNaira - earningKeep;
            var platformClawback = payment.CommissionNaira - commissionKeep;
            if (platformClawback > 0)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                    WalletTransactionType.Adjustment, -platformClawback,
                    booking.Id, payment.Id, $"Commission reversal for the partial refund on booking {booking.Id}", now));
            if (payment.ArtisanId is { } aid && artisanClawback > 0)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Artisan, aid,
                    WalletTransactionType.Adjustment, -artisanClawback,
                    booking.Id, payment.Id, $"Earning reversal for the partial refund on booking {booking.Id}", now));
        }
        else
        {
            // Escrow was still held: release only the kept portion to the artisan +
            // platform (the refunded portion goes back to the customer, above).
            if (commissionKeep > 0)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                    WalletTransactionType.PlatformCommission, commissionKeep,
                    booking.Id, payment.Id, $"Commission on booking {booking.Id} (partial)", now));
            var earningToRelease = Math.Max(0, earningKeep - advance); // advance already in their wallet
            if (payment.ArtisanId is { } aid && earningToRelease > 0)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Artisan, aid,
                    WalletTransactionType.ArtisanEarning, earningToRelease,
                    booking.Id, payment.Id, $"Earning for booking {booking.Id} (partial)", now));
            payment.MarkEarningReleased(now); // the kept portion is now attributed
        }

        payment.MarkPartiallyRefunded(refundNaira, now);
        booking.MarkPartiallyRefunded(refundNaira);
        _notifications.RefundIssued(booking, refundNaira);
        await _gateway.RefundAsync(payment.Reference, refundNaira, ct);

        return refundNaira;
    }
}
