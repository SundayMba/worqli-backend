namespace Servika.Contracts.Reviews;

/// <summary>
/// A review as the client sees it — on the artisan profile's reviews list and as
/// the "your review" record on a completed booking. Immutable; no user id leaves
/// here, only the display name.
/// </summary>
public sealed record ReviewDto(
    Guid Id,
    Guid BookingId,
    Guid ArtisanId,
    string CustomerName,
    int Rating,
    string? Comment,
    string ServiceName,
    DateTimeOffset CreatedAt);
