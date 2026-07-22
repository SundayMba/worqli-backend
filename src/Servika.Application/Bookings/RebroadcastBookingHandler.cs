using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Turns a stalled direct request into an open broadcast: when the chosen
/// artisan declined (Rejected) or hasn't responded (Pending), one tap re-offers
/// the same job — description, media, schedule intact — to every matching
/// artisan, exactly like posting an open request from scratch.
/// </summary>
public sealed class RebroadcastBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly Notifications.NotificationEmitter _notifications;

    public RebroadcastBookingHandler(
        IBookingRepository bookings, Notifications.NotificationEmitter notifications)
    {
        _bookings = bookings;
        _notifications = notifications;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        booking.Rebroadcast();

        // Same fan-out as a fresh open request — matching artisans hear about it.
        await _notifications.OpenJobPosted(booking, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }
}
