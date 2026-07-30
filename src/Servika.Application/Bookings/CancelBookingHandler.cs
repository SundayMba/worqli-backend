using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Cancels one of the customer's own bookings. Ownership is enforced (a foreign
/// booking is a 404); the state-machine rule about when cancelling is allowed
/// lives in the <c>Booking</c> entity, which throws if the transition is illegal.
/// If escrow was already paid, it is refunded in full (ledger reversal), in the
/// same transaction as the cancellation.
/// </summary>
public sealed class CancelBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly Payments.RefundService _refunds;
    private readonly IClock _clock;

    public CancelBookingHandler(
        IBookingRepository bookings,
        Notifications.NotificationEmitter notifications,
        Payments.RefundService refunds,
        IClock clock)
    {
        _bookings = bookings;
        _notifications = notifications;
        _refunds = refunds;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        booking.Cancel(_clock.UtcNow);
        // Money already held in escrow goes straight back (full refund; no-op if
        // unpaid). Stages onto the same DbContext → one transaction with the cancel.
        await _refunds.RefundIfPaidAsync(booking, ct);
        // Let the assigned artisan know it's off (no-op for an open/unassigned booking).
        await _notifications.ArtisanBookingCancelled(booking, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }
}
