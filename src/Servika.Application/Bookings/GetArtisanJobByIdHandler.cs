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
    private readonly IUserRepository _users;

    public GetArtisanJobByIdHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue, IUserRepository users)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _users = users;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        // The artisan is heading to a person, not a "Customer" — resolve the
        // name for the job screen + the live map's destination tag.
        var customer = await _users.FindByIdAsync(booking.CustomerId, ct);
        var completed = await _bookings.CountCompletedForCustomerAsync(booking.CustomerId, ct);

        return booking.ToDetailDto(customerName: customer?.FullName, customerCompletedJobs: completed);
    }
}
