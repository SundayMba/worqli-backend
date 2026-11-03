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
/// <para>A booking can hold more than one payment (a diagnosis fee, then the approved
/// repairs); every one of them is refunded. Stages the changes only (no
/// <c>SaveChanges</c>), so it commits inside the caller's transaction. A no-op when the
/// booking was never paid, and idempotent — only a <see cref="PaymentStatus.Succeeded"/>
/// payment can be refunded, once.</para>
/// </summary>
public sealed class RefundService
{
    private readonly IPaymentRepository _payments;
    private readonly IWalletRepository _wallet;
    private readonly NotificationEmitter _notifications;
    private readonly IPaymentGateway _gateway;
    private readonly EscrowReleaseService _escrow;
    private readonly IClock _clock;

    public RefundService(
        IPaymentRepository payments,
        IWalletRepository wallet,
        NotificationEmitter notifications,
        IPaymentGateway gateway,
        EscrowReleaseService escrow,
        IClock clock)
    {
        _payments = payments;
        _wallet = wallet;
        _notifications = notifications;
        _gateway = gateway;
        _escrow = escrow;
        _clock = clock;
    }

    private async Task<List<Payment>> RefundableAsync(Booking booking, CancellationToken ct) =>
        (await _payments.ListSucceededForBookingAsync(booking.Id, ct)).Where(p => !p.IsRefundRequested).ToList();

    /// <summary>Fully refunds every settled payment on the booking. Returns the refunded
    /// amount (0 if nothing was paid).</summary>
    public async Task<int> RefundIfPaidAsync(Booking booking, CancellationToken ct)
    {
        var payments = await RefundableAsync(booking, ct);
        if (payments.Count == 0) return 0; // never paid / already refunded

        // A materials advance the customer explicitly released is theirs no longer:
        // the artisan bought the parts with it. Everything else comes back.
        var advance = booking.ReleasedMaterialsAdvanceNaira;
        if (advance > 0)
            return await PartialRefundAsync(booking, payments, payments.Sum(p => p.AmountNaira) - advance, ct, refundAll: true);

        var total = 0;
        foreach (var payment in payments) total += await RefundWholeAsync(booking, payment, ct);
        booking.MarkRefunded(total);
        _notifications.RefundIssued(booking, total);
        return total;
    }

    /// <summary>Refunds one payment in full and reverses whatever of it was already released.</summary>
    private async Task<int> RefundWholeAsync(Booking booking, Payment payment, CancellationToken ct)
    {
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

        // The payment fee the customer paid on top comes back with a full refund: the
        // dispute went their way, so they should not be out of pocket for paying online.
        // Servika absorbs it (the gateway keeps its cut regardless).
        if (payment.ServiceFeeNaira > 0)
        {
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Customer, payment.CustomerId,
                WalletTransactionType.ServiceFee, payment.ServiceFeeNaira,
                booking.Id, payment.Id, "Payment fee returned with the refund", now));
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.ServiceFee, -payment.ServiceFeeNaira,
                booking.Id, payment.Id, $"Payment fee returned on booking {booking.Id}", now));
        }

        payment.MarkRefunded(now);
        await _gateway.RefundAsync(payment.Reference, payment.ChargedNaira, ct);
        return payment.AmountNaira;
    }

    /// <summary>Partially refunds a paid booking: the customer gets
    /// <paramref name="refundNaira"/> back and the artisan keeps the rest as their
    /// earning for work rendered. Used when an admin resolves a dispute with a partial
    /// refund. Falls back to a full refund if the amount covers (or exceeds) everything
    /// paid. Returns the amount refunded to the customer (0 if nothing was paid).
    ///
    /// <para>With several payments the refund is taken from the newest first (the repairs before the
    /// diagnosis fee). Whatever is kept is settled to the artisan here (released from escrow if it was
    /// still held, or clawed down if it had already been released), so every balance nets correctly.</para>
    /// </summary>
    public async Task<int> PartialRefundIfPaidAsync(Booking booking, int refundNaira, CancellationToken ct)
    {
        var payments = await RefundableAsync(booking, ct);
        if (payments.Count == 0) return 0;
        return await PartialRefundAsync(booking, payments, refundNaira, ct, refundAll: false);
    }

    private async Task<int> PartialRefundAsync(
        Booking booking, List<Payment> payments, int refundNaira, CancellationToken ct, bool refundAll)
    {
        var full = payments.Sum(p => p.AmountNaira);
        // A released materials advance can never be refunded (see RefundIfPaidAsync).
        var advance = booking.ReleasedMaterialsAdvanceNaira;
        refundNaira = Math.Min(refundNaira, full - advance);
        if (!refundAll && refundNaira >= full) return await RefundIfPaidAsync(booking, ct); // whole thing
        if (refundNaira <= 0) return 0;

        // Newest first: the repairs come back before the diagnosis fee.
        var remaining = refundNaira;
        foreach (var payment in payments.OrderByDescending(p => p.CreatedAt))
        {
            var share = Math.Min(remaining, payment.AmountNaira);
            remaining -= share;
            if (share == 0)
                _escrow.Release(booking, payment);            // kept whole: the artisan earns it
            else if (share == payment.AmountNaira && payments.Count > 1)
                await RefundWholeAsync(booking, payment, ct);
            else
                await RefundPartOfAsync(booking, payment, share, payments.Count == 1 ? advance : 0, ct);
        }

        booking.MarkPartiallyRefunded(refundNaira);
        _notifications.RefundIssued(booking, refundNaira);
        return refundNaira;
    }

    /// <summary>Refunds part of one payment and settles the kept part to the artisan.</summary>
    private async Task RefundPartOfAsync(Booking booking, Payment payment, int refundNaira, int advance, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var keep = payment.AmountNaira - refundNaira;   // the artisan's portion (incl. any advance)
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
            // Escrow was still held: release only the kept portion to the artisan who did the job +
            // the platform (the refunded portion goes back to the customer, above).
            if (booking.ArtisanId is { } doneBy && payment.ArtisanId != doneBy)
                payment.AttributeTo(doneBy);
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
        await _gateway.RefundAsync(payment.Reference, refundNaira, ct);
    }
}
