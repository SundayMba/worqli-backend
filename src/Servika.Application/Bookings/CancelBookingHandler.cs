using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Cancels one of the customer's own bookings. Ownership is enforced (a foreign
/// booking is a 404); the state-machine rule about when cancelling is allowed
/// lives in the <c>Booking</c> entity, which throws if the transition is illegal.
/// </summary>
public sealed class CancelBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    public CancelBookingHandler(
        IBookingRepository bookings, Notifications.NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        booking.Cancel(_clock.UtcNow);
        // Let the assigned artisan know it's off (no-op for an open/unassigned booking).
        await _notifications.ArtisanBookingCancelled(booking, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }
}
