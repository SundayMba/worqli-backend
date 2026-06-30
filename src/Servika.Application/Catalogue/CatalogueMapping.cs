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

    public static ArtisanSummaryDto ToSummaryDto(this ArtisanProfile a) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            a.DistanceKm, a.IsAvailable, a.Accent);

    public static ArtisanDetailDto ToDetailDto(this ArtisanProfile a) =>
        new(a.Id, a.ImageKey, a.FullName, a.Specialty, a.Rating, a.ReviewCount,
            a.DistanceKm, a.IsAvailable, a.Accent, a.ExperienceYears, a.Location,
            a.ResponseTime, a.JobsCount, a.InspectionFeeNaira, a.About,
            a.Services, a.GalleryKeys, a.CategorySlugs);
}
