namespace Servika.Contracts.Catalogue;

/// <summary>
/// The full artisan profile (GET /api/v1/artisans/{id}). Extends the summary
/// fields with the details the profile screen needs. The client resolves the
/// avatar/cover from <see cref="ImageKey"/> and the gallery from
/// <see cref="GalleryKeys"/>; <see cref="InspectionFeeNaira"/> is formatted on
/// the client (e.g. "₦5,000").
/// </summary>
public sealed record ArtisanDetailDto(
    Guid Id,
    string ImageKey,
    string FullName,
    string Specialty,
    double Rating,
    int ReviewCount,
    double DistanceKm,
    bool IsAvailable,
    string Accent,
    int ExperienceYears,
    string Location,
    string ResponseTime,
    string JobsCount,
    int InspectionFeeNaira,
    string About,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> GalleryKeys);
