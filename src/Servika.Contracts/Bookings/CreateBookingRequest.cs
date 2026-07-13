namespace Servika.Contracts.Bookings;

/// <summary>
/// Submit a new booking (POST /api/v1/bookings). Mirrors what the mobile booking
/// flow collects across its three steps (service + details, location, confirm).
/// <paramref name="Urgency"/> is "standard" or "urgent" (case-insensitive).
/// Pricing model and commission are decided server-side, never trusted from the
/// client.
/// </summary>
public sealed record CreateBookingRequest(
    string CategorySlug,
    Guid? ArtisanId,
    string Description,
    DateTimeOffset PreferredDate,
    string PreferredTimeSlot,
    string Urgency,
    string AddressText,
    double? LocationLat,
    double? LocationLng,
    string? LocationInstructions,
    /// <summary>"Inspection" (artisan comes to check &amp; discuss the price —
    /// default) or "RemoteQuote" (priceable from photos/video → artisans bid).
    /// Only meaningful on an open request (no pre-selected artisan).</summary>
    string? AssessmentMode = null,
    /// <summary>Job photos (base64 / data: URIs, max 4) giving artisans context.
    /// Required context when bidding.</summary>
    List<string>? MediaBase64 = null,
    /// <summary>A short video clip of the job (base64), so bidding artisans can
    /// assess the work properly. Optional.</summary>
    string? VideoBase64 = null);
