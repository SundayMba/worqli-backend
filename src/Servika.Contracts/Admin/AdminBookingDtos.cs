namespace Servika.Contracts.Admin;

/// <summary>A booking as the admin monitor lists it (adds the customer's name).</summary>
public sealed record AdminBookingDto(
    Guid Id,
    string Status,
    string ServiceName,
    string? ArtisanName,
    string CustomerName,
    string AddressText,
    DateTimeOffset PreferredDate,
    string PreferredTimeSlot,
    string Urgency,
    string PaymentState,
    int? AmountNaira,
    DateTimeOffset CreatedAt,
    /// <summary>"Inspection" (quote after visit) or "RemoteQuote" (bidding).</summary>
    string AssessmentMode,
    /// <summary>"Online" (escrow) or "Cash" (paid after service).</summary>
    string PaymentMethod);

/// <summary>The full admin view of one booking — parties, service, money, timeline.</summary>
public sealed record AdminBookingDetailDto(
    Guid Id,
    string Status,
    string ServiceName,
    string CategorySlug,
    string Description,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string? ArtisanName,
    string? ArtisanPhone,
    double? ArtisanRating,
    int? ArtisanReviewCount,
    string AddressText,
    string? LocationInstructions,
    DateTimeOffset PreferredDate,
    string PreferredTimeSlot,
    string Urgency,
    int? ServiceAmountNaira,
    int CommissionNaira,
    decimal CommissionRate,
    string PaymentState,
    /// <summary>"Online" (escrow) or "Cash" (paid after service).</summary>
    string PaymentMethod,
    /// <summary>"Fixed" (published price) or "Variable" (quoted).</summary>
    string PricingModel,
    /// <summary>"Inspection" or "RemoteQuote" (bidding).</summary>
    string AssessmentMode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? WorkSubmittedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? DisputedAtUtc);
