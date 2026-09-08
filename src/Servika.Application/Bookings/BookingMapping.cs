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
            b.CreatedAt,
            b.Assessment.ToString(),
            b.MediaUrls(),
            b.VideoUrl(),
            b.LocationLat,
            b.LocationLng);

    public static BookingDetailDto ToDetailDto(
        this Booking b, int bidCount = 0, string? customerName = null, int? customerCompletedJobs = null) =>
        new(
            b.Id,
            b.Status.ToString(),
            b.CustomerId,
            b.ArtisanId,
            b.CategorySlug,
            b.ServiceName,
            b.ArtisanName,
            customerName,
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
            b.PaymentMethod.ToString(),
            b.InitialQuoteAmountNaira,
            b.RefundedAmountNaira,
            b.CommissionRate,
            b.CreatedAt,
            b.AcceptedAtUtc,
            b.CompletedAtUtc,
            b.CancelledAtUtc,
            b.WorkStartedAtUtc,
            b.Assessment.ToString(),
            b.MediaUrls(),
            b.VideoUrl(),
            bidCount,
            b.AgreedWorkmanshipNaira,
            b.AgreedMaterialsNaira,
            b.MaterialsAdvanceStatus.ToString(),
            b.MaterialsAdvanceNaira,
            customerCompletedJobs);

    /// <summary>API paths of the customer's job photos.</summary>
    private static IReadOnlyList<string> MediaUrls(this Booking b) =>
        b.MediaKeys.Select(k => $"/api/v1/bookings/{b.Id}/media/{k}").ToList();

    /// <summary>API path of the customer's job video clip, or null.</summary>
    private static string? VideoUrl(this Booking b) =>
        string.IsNullOrEmpty(b.VideoKey) ? null : $"/api/v1/bookings/{b.Id}/media/{b.VideoKey}";
}
