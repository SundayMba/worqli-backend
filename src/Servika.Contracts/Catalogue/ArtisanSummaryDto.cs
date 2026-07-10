namespace Servika.Contracts.Catalogue;

/// <summary>
/// The compact artisan card shown in the home "Nearby Artisans" carousel and the
/// category listing (GET /api/v1/artisans, GET /api/v1/categories/{slug}/artisans).
/// The client resolves the avatar from <see cref="ImageKey"/>.
/// </summary>
public sealed record ArtisanSummaryDto(
    Guid Id,
    string ImageKey,
    string FullName,
    string Specialty,
    double Rating,
    int ReviewCount,
    double DistanceKm,
    bool IsAvailable,
    string Accent,
    double? Latitude,
    double? Longitude);
