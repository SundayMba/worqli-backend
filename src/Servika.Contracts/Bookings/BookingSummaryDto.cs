namespace Servika.Contracts.Bookings;

/// <summary>
/// A booking as it appears in the customer's "My Bookings" history list
/// (GET /api/v1/bookings). Compact — the detail screen fetches the full record.
/// </summary>
public sealed record BookingSummaryDto(
    Guid Id,
    string Status,
    string ServiceName,
    string? ArtisanName,
    string AddressText,
    DateTimeOffset PreferredDate,
    string PreferredTimeSlot,
    string Urgency,
    int? AmountNaira,
    DateTimeOffset CreatedAt,
    /// <summary>"Inspection" or "RemoteQuote" — RemoteQuote open requests take
    /// bids instead of first-come claims.</summary>
    string AssessmentMode,
    /// <summary>API paths of the customer's job photos (context for artisans).</summary>
    IReadOnlyList<string> MediaUrls,
    /// <summary>API path of the customer's short job video, or null.</summary>
    string? VideoUrl,
    /// <summary>Job coordinates, when the customer pinned one — lets the Pro app
    /// show distance from the artisan's base on list cards.</summary>
    double? LocationLat = null,
    double? LocationLng = null);
