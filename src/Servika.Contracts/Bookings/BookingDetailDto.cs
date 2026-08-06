namespace Servika.Contracts.Bookings;

/// <summary>
/// The full booking record for the detail screen (GET /api/v1/bookings/{id} and
/// the create response). <c>Status</c>, <c>Urgency</c> and <c>PricingModel</c>
/// are readable strings.
/// </summary>
public sealed record BookingDetailDto(
    Guid Id,
    string Status,
    Guid CustomerId,
    Guid? ArtisanId,
    string CategorySlug,
    string ServiceName,
    string? ArtisanName,
    /// <summary>The customer's display name — filled on the ARTISAN-side job
    /// detail (drives the map's name tag); null on customer-side responses,
    /// where the caller is the customer.</summary>
    string? CustomerName,
    string Description,
    string AddressText,
    double? LocationLat,
    double? LocationLng,
    string? LocationInstructions,
    DateTimeOffset PreferredDate,
    string PreferredTimeSlot,
    string Urgency,
    string PricingModel,
    string PaymentState,
    /// <summary>"Online" (escrow, default) or "Cash" (pay after service).</summary>
    string PaymentMethod,
    int? InitialQuoteAmountNaira,
    /// <summary>Amount returned to the customer if refunded (full or partial), else null.</summary>
    int? RefundedAmountNaira,
    decimal CommissionRate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    /// <summary>When the artisan started the work (drives the job timer).</summary>
    DateTimeOffset? WorkStartedAtUtc,
    /// <summary>"Inspection" or "RemoteQuote" (bidding).</summary>
    string AssessmentMode,
    /// <summary>API paths of the customer's job photos.</summary>
    IReadOnlyList<string> MediaUrls,
    /// <summary>API path of the customer's short job video, or null.</summary>
    string? VideoUrl,
    /// <summary>Active bids on this request (RemoteQuote + Open only; 0 otherwise).</summary>
    int BidCount);
