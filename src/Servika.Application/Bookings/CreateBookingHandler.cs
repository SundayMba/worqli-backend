using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Submits a new booking for the signed-in customer. Validates the chosen
/// category (and artisan, if one was pre-selected) against the catalogue, then
/// records the booking in <see cref="BookingStatus.Pending"/>.
///
/// Pricing is decided here, never trusted from the client: we default to
/// <see cref="PricingModel.Variable"/> (most home-repair jobs), carry the chosen
/// artisan's inspection fee as the up-front amount, and stamp the commission rate
/// from the admin-controlled <see cref="Servika.Domain.Settings.PlatformSettings"/>
/// (standard vs emergency by urgency) so monetisation is a settings change, not a
/// redeploy. Defaults to 0 during the launch window.
/// </summary>
public sealed class CreateBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IPlatformSettingsRepository _settings;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    public CreateBookingHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IPlatformSettingsRepository settings,
        Notifications.NotificationEmitter notifications,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _settings = settings;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, CreateBookingRequest request, CancellationToken ct)
    {
        var slug = request.CategorySlug?.Trim() ?? string.Empty;
        var category = await _catalogue.GetCategoryBySlugAsync(slug, ct)
            ?? throw new NotFoundException($"Category '{slug}' was not found.");

        string? artisanName = null;
        int? initialAmount = null;
        if (request.ArtisanId is { } artisanId)
        {
            var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
                ?? throw new NotFoundException($"Artisan '{artisanId}' was not found.");
            artisanName = artisan.FullName;
            initialAmount = artisan.InspectionFeeNaira;
        }

        // Commission comes from admin settings — emergency rate for urgent jobs.
        var urgency = ParseUrgency(request.Urgency);
        var settings = await _settings.GetOrCreateAsync(ct);
        var commissionRate = urgency == Urgency.Urgent
            ? settings.EmergencyCommissionRate
            : settings.CommissionRate;

        var booking = Booking.Create(
            customerId: customerId,
            artisanId: request.ArtisanId,
            categorySlug: category.Slug,
            serviceName: category.Name,
            artisanName: artisanName,
            description: request.Description,
            addressText: request.AddressText,
            locationLat: request.LocationLat,
            locationLng: request.LocationLng,
            locationInstructions: request.LocationInstructions,
            preferredDate: request.PreferredDate,
            preferredTimeSlot: request.PreferredTimeSlot,
            urgency: urgency,
            pricingModel: PricingModel.Variable,
            initialQuoteAmountNaira: initialAmount,
            commissionRate: commissionRate,
            now: _clock.UtcNow);

        _bookings.Add(booking);
        // Let the assigned artisan know a job came in (no-op for an open booking).
        await _notifications.ArtisanNewBooking(booking, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }

    private static Urgency ParseUrgency(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "urgent" => Urgency.Urgent,
            "standard" or null or "" => Urgency.Standard,
            _ => throw new ArgumentException(
                "Urgency must be 'standard' or 'urgent'.", nameof(value)),
        };
}
