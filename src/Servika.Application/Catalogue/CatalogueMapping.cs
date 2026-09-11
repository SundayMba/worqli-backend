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

    /// <param name="distanceKmOverride">Real distance from the customer's
    /// location, when supplied; otherwise the artisan's seeded baseline distance.</param>
    public static ArtisanSummaryDto ToSummaryDto(this ArtisanProfile a, double? distanceKmOverride = null) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            distanceKmOverride ?? a.DistanceKm, a.IsAvailable, a.Accent,
            a.Latitude, a.Longitude, a.PhotoUrl(), a.HasCertificate);

    public static ArtisanDetailDto ToDetailDto(
        this ArtisanProfile a, IReadOnlyList<ArtisanServiceDto>? pricedServices = null) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            a.DistanceKm, a.IsAvailable, a.Accent, a.ExperienceYears, a.Location,
            a.ResponseTime, a.JobsCount, a.InspectionFeeNaira, a.About,
            a.Services, a.GalleryKeys, a.CategorySlugs, a.PhotoUrl(), a.CoverPhotoUrl(),
            a.GalleryUrls(), a.HasCertificate, pricedServices ?? Array.Empty<ArtisanServiceDto>());

    public static MyArtisanProfileDto ToMyProfileDto(this ArtisanProfile a, int guarantorCount = 0, bool guarantorsRequired = true, int requiredGuarantorCount = 2) =>
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
