using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// Maps catalogue Domain entities to the public Contracts DTOs. Keeps the
/// translation in one place so handlers stay focused on the use case.
/// </summary>
internal static class CatalogueMapping
{
    public static CategoryDto ToDto(this ServiceCategory c) =>
        new(c.Id, c.Slug, c.Name, c.Tint, c.IconKey, c.SortOrder, c.IsPopular);

    /// <param name="distanceKm">Real distance from the caller's coordinates, or null
    /// when either side's location is unknown (never the stored baseline).</param>
    public static ArtisanSummaryDto ToSummaryDto(this ArtisanProfile a, double? distanceKm, int completedJobs) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            distanceKm, a.IsAvailable, a.Accent,
            a.Latitude, a.Longitude, a.PhotoUrl(), a.HasCertificate,
            a.VerificationStatus == ArtisanVerificationStatus.Verified, completedJobs);

    public static ArtisanDetailDto ToDetailDto(
        this ArtisanProfile a, double? distanceKm, int completedJobs, IReadOnlyList<ArtisanServiceDto> pricedServices) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            distanceKm, a.IsAvailable, a.Accent, a.ExperienceYears, a.Location,
            string.Empty, completedJobs.ToString(), a.InspectionFeeNaira, a.About,
            a.Services, a.GalleryKeys, a.CategorySlugs, a.PhotoUrl(), a.CoverPhotoUrl(),
            a.GalleryUrls(), a.HasCertificate, pricedServices,
            a.VerificationStatus == ArtisanVerificationStatus.Verified, completedJobs);

    /// <summary>Km between the caller and the artisan's base, rounded to 0.1; null
    /// when either location is unknown.</summary>
    public static double? DistanceFrom(this ArtisanProfile a, double? lat, double? lng) =>
        lat is { } la && lng is { } ln && a is { Latitude: { } alat, Longitude: { } alng }
            ? Math.Round(GeoDistance.Km(la, ln, alat, alng), 1)
            : null;

    public static MyArtisanProfileDto ToMyProfileDto(this ArtisanProfile a, int guarantorCount, bool guarantorsRequired, int requiredGuarantorCount) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            a.IsAvailable, a.VerificationStatus.ToString(), a.ExperienceYears,
            a.Location, a.InspectionFeeNaira, a.About, a.CategorySlugs, a.Services,
            a.PhotoUrl(), a.CoverPhotoUrl(), a.GalleryUrls(), a.HasCertificate,
            a.Latitude, a.Longitude,
            a.PayoutBankCode, a.PayoutBankName,
            a.PayoutAccountNumber is { Length: >= 4 } acct ? $"••••{acct[^4..]}" : null,
            a.PayoutAccountName,
            a.WorkRadiusKm, a.AcceptsEmergency, a.WorkingHoursJson, a.AwayUntilUtc,
            guarantorCount, guarantorsRequired, requiredGuarantorCount, a.NinLookupStatus, a.NinLookupName);

    /// <summary>API path the clients load the uploaded photo from, or null if
    /// none was uploaded (clients fall back to the bundled ImageKey art).</summary>
    private static string? PhotoUrl(this ArtisanProfile a) =>
        string.IsNullOrEmpty(a.PhotoKey) ? null : $"/api/v1/artisans/{a.Id}/photo";

    private static string? CoverPhotoUrl(this ArtisanProfile a) =>
        string.IsNullOrEmpty(a.CoverPhotoKey) ? null : $"/api/v1/artisans/{a.Id}/cover";

    /// <summary>API paths of the uploaded work-gallery photos, newest first.</summary>
    internal static IReadOnlyList<string> GalleryUrls(this ArtisanProfile a) =>
        a.GalleryPhotoKeys
            .Select(k => $"/api/v1/artisans/{a.Id}/gallery/{k}")
            .ToList();
}
