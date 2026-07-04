namespace Servika.Contracts.Disputes;

/// <summary>
/// A dispute record. <see cref="Status"/> ("Open" / "UnderReview" / "Resolved")
/// and <see cref="Resolution"/> ("None" / "FavourCustomer" / "FavourArtisan") are
/// readable strings the apps map to chips.
/// </summary>
public sealed record DisputeDto(
    Guid Id,
    Guid BookingId,
    string CustomerName,
    string ServiceName,
    string Category,
    string Description,
    string Status,
    string Resolution,
    string? ResolutionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAtUtc);

/// <summary>
/// Raise a dispute for a booking (POST /api/v1/bookings/{id}/dispute). Category is
/// the issue type ("quality" / "no-show" / "payment" / "unsafe" / "other").
/// </summary>
public sealed record RaiseDisputeRequest(
    string Category,
    string Description);

/// <summary>
/// Admin resolves a dispute (POST /api/v1/admin/disputes/{id}/resolve).
/// <see cref="Outcome"/> is "customer" (refund/cancel) or "artisan" (work stands).
/// </summary>
public sealed record ResolveDisputeRequest(
    string Outcome,
    string? Note);
