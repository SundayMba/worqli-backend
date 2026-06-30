using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Returns one booking's full detail, but only if it belongs to the requesting
/// customer — another customer's id is a 404, never a leak.
/// </summary>
public sealed class GetBookingByIdHandler
{
    private readonly IBookingRepository _bookings;

    public GetBookingByIdHandler(IBookingRepository bookings)
    {
        _bookings = bookings;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        return booking.ToDetailDto();
    }
}
