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
/// Pricing is decided here, never trusted from the client: until per-category
/// pricing config arrives with the payments slice we default to
/// <see cref="PricingModel.Variable"/> (most home-repair jobs), carry the chosen
/// artisan's inspection fee as the up-front amount, and stamp a 0 commission rate
/// (the launch window) so the ledger basis exists from day one.
/// </summary>
public sealed class CreateBookingHandler
{
    private const decimal LaunchCommissionRate = 0m;

    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IClock _clock;

    public CreateBookingHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
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
            urgency: ParseUrgency(request.Urgency),
            pricingModel: PricingModel.Variable,
            initialQuoteAmountNaira: initialAmount,
            commissionRate: LaunchCommissionRate,
            now: _clock.UtcNow);

        _bookings.Add(booking);
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
