namespace Servika.Contracts.Catalogue;

/// <summary>
/// How many checked artisans could take a job at a point (GET /api/v1/availability). The app shows
/// "N artisans available near you" only above a threshold, and "not in your area yet" when nobody covers it.
/// </summary>
public sealed record AvailabilityDto(
    /// <summary>Verified, online, not away, and the point is inside their work radius.</summary>
    int AvailableNow,
    /// <summary>Verified and not away with the point inside their radius, online or not. 0 = not in your area yet.</summary>
    int Covering);

/// <summary>POST /api/v1/service-interest: a service or area Servika does not offer yet.</summary>
public sealed record RecordServiceInterestRequest(
    string? CategorySlug,
    string? Note,
    string? AreaLabel,
    double? Latitude,
    double? Longitude);

/// <summary>One recorded request, for the admin (GET /api/v1/admin/service-interest).</summary>
public sealed record ServiceInterestDto(
    Guid Id,
    string? CategorySlug,
    string? Note,
    string? AreaLabel,
    double? Latitude,
    double? Longitude,
    bool SignedIn,
    DateTimeOffset CreatedAtUtc);
