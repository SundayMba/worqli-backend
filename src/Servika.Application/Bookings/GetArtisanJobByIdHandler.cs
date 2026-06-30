using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Returns one job assigned to the signed-in artisan in full. A job that isn't
/// assigned to this artisan's profile is a 404 — never a leak — mirroring the
/// customer's <c>GetBookingByIdHandler</c>.
/// </summary>
public sealed class GetArtisanJobByIdHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;

    public GetArtisanJobByIdHandler(IBookingRepository bookings, ICatalogueRepository catalogue)
    {
        _bookings = bookings;
        _catalogue = catalogue;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        return booking.ToDetailDto();
    }
}
