using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Builds the full ARTISAN-facing booking detail (customer name, the customer's
/// completed-job count, the artisan's take-home and the auto-confirm deadline) so
/// every artisan read and mutation returns the same complete shape.
/// </summary>
public sealed class ArtisanBookingDtoBuilder
{
    private readonly IUserRepository _users;
    private readonly IBookingRepository _bookings;
    private readonly IPlatformSettingsRepository _settings;

    public ArtisanBookingDtoBuilder(
        IUserRepository users, IBookingRepository bookings, IPlatformSettingsRepository settings)
    {
        _users = users;
        _bookings = bookings;
        _settings = settings;
    }

    public async Task<BookingDetailDto> BuildAsync(Booking booking, int bidCount, CancellationToken ct)
    {
        var customer = await _users.FindByIdAsync(booking.CustomerId, ct);
        var completed = await _bookings.CountCompletedForCustomerAsync(booking.CustomerId, ct);
        var settings = await _settings.GetOrCreateAsync(ct);
        return booking.ToDetailDto(
            bidCount, settings.AutoConfirmHours, new ArtisanSideView(customer?.FullName, completed));
    }
}
