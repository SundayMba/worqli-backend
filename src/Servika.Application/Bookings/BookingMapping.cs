using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Maps booking Domain entities to the public Contracts DTOs. Enums become
/// readable strings, matching how the rest of the API serialises them.
/// </summary>
internal static class BookingMapping
{
    public static BookingSummaryDto ToSummaryDto(this Booking b) =>
        new(
            b.Id,
            b.Status.ToString(),
            b.ServiceName,
            b.ArtisanName,
            b.AddressText,
            b.PreferredDate,
            b.PreferredTimeSlot,
            b.Urgency.ToString(),
            b.InitialQuoteAmountNaira,
            b.CreatedAt);

    public static BookingDetailDto ToDetailDto(this Booking b) =>
        new(
            b.Id,
            b.Status.ToString(),
            b.CustomerId,
            b.ArtisanId,
            b.CategorySlug,
            b.ServiceName,
            b.ArtisanName,
            b.Description,
            b.AddressText,
            b.LocationLat,
            b.LocationLng,
            b.LocationInstructions,
            b.PreferredDate,
            b.PreferredTimeSlot,
            b.Urgency.ToString(),
            b.PricingModel.ToString(),
            b.PaymentState.ToString(),
            b.InitialQuoteAmountNaira,
            b.CommissionRate,
            b.CreatedAt,
            b.AcceptedAtUtc,
            b.CompletedAtUtc,
            b.CancelledAtUtc);
}
