using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// "Ask other artisans": the customer gives up on the artisan they asked for (unanswered or
/// declined) and Servika offers the same job, description, photos and schedule intact, to the
/// next artisan in rotation. Same route as the old re-broadcast so the apps keep working.
/// </summary>
public sealed class RebroadcastBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly AssignmentService _assignment;
    private readonly IClock _clock;

    public RebroadcastBookingHandler(IBookingRepository bookings, AssignmentService assignment, IClock clock)
    {
        _bookings = bookings;
        _assignment = assignment;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        await _assignment.ReassignAsync(booking, null, null, _clock.UtcNow, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto(0, null, null);
    }
}
