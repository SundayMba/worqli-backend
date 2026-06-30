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
    int? InitialQuoteAmountNaira,
    decimal CommissionRate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc);
