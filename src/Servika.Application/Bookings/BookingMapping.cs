using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>Artisan-side extras on a booking DTO: who the customer is, and that the
/// artisan's own take-home should be shown.</summary>
internal sealed record ArtisanSideView(string? CustomerName, int? CustomerCompletedJobs);

/// <summary>
/// Maps booking Domain entities to the public Contracts DTOs. Enums become
/// readable strings, matching how the rest of the API serialises them.
/// </summary>
internal static class BookingMapping
{
    /// <param name="artisanSide">True on ARTISAN-facing responses: fills
    /// <c>artisanNetNaira</c>. Customer-facing responses leave it null.</param>
    public static BookingSummaryDto ToSummaryDto(this Booking b, bool artisanSide) =>
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
            b.LocationLng,
            b.PaymentMethod.ToString(),
            b.PaymentState.ToString(),
            b.CompletedAtUtc,
            artisanSide ? b.ArtisanNetNaira() : null);

    /// <param name="bidCount">Active bids to show (0 when not relevant).</param>
    /// <param name="autoConfirmHours">The admin auto-confirm window, when the caller
    /// loaded it; drives <c>autoConfirmAtUtc</c> for AwaitingConfirmation jobs.</param>
    /// <param name="artisanSide">Non-null on ARTISAN-facing responses.</param>
    public static BookingDetailDto ToDetailDto(
        this Booking b, int bidCount, int? autoConfirmHours, ArtisanSideView? artisanSide) =>
        new(
            b.Id,
            b.Status.ToString(),
            b.CustomerId,
            b.ArtisanId,
            b.CategorySlug,
            b.ServiceName,
            b.ArtisanName,
            artisanSide?.CustomerName,
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
            artisanSide?.CustomerCompletedJobs,
            artisanSide is null ? null : b.ArtisanNetNaira(),
            b.AutoConfirmAtUtc(autoConfirmHours));

    /// <summary>What the artisan actually receives for the job: the agreed amount
    /// minus Servika's commission at the booking's rate (rounded exactly like
    /// <c>Payment.CommissionNaira</c>) minus any materials advance already released.
    /// Null when no amount is agreed yet.</summary>
    public static int? ArtisanNetNaira(this Booking b)
    {
        if (b.InitialQuoteAmountNaira is not { } amount) return null;
        var commission = (int)Math.Round(amount * b.CommissionRate, MidpointRounding.AwayFromZero);
        return Math.Max(0, amount - commission - b.ReleasedMaterialsAdvanceNaira);
    }

    /// <summary>When an AwaitingConfirmation job confirms itself, or null.</summary>
    public static DateTimeOffset? AutoConfirmAtUtc(this Booking b, int? autoConfirmHours) =>
        b.Status == BookingStatus.AwaitingConfirmation
        && b.WorkSubmittedAtUtc is { } submitted
        && autoConfirmHours is { } hours
            ? submitted.AddHours(hours)
            : null;

    /// <summary>API paths of the customer's job photos.</summary>
    private static IReadOnlyList<string> MediaUrls(this Booking b) =>
        b.MediaKeys.Select(k => $"/api/v1/bookings/{b.Id}/media/{k}").ToList();

    /// <summary>API path of the customer's job video clip, or null.</summary>
    private static string? VideoUrl(this Booking b) =>
        string.IsNullOrEmpty(b.VideoKey) ? null : $"/api/v1/bookings/{b.Id}/media/{b.VideoKey}";
}
