using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Read access to the marketplace catalogue (categories + artisan profiles).
/// Stated in domain terms; Infrastructure implements it with EF Core. All
/// queries are read-only for the catalogue slice.
/// </summary>
public interface ICatalogueRepository
{
    /// <summary>Active categories in display order.</summary>
    Task<IReadOnlyList<ServiceCategory>> GetCategoriesAsync(CancellationToken ct);

    /// <summary>True if a category with this slug exists and is active.</summary>
    Task<bool> CategoryExistsAsync(string slug, CancellationToken ct);

    /// <summary>A single active category by slug, or null — used when a booking
    /// needs the category's display name.</summary>
    Task<ServiceCategory?> GetCategoryBySlugAsync(string slug, CancellationToken ct);

    /// <summary>Artisan summaries, optionally filtered to one category slug.</summary>
    Task<IReadOnlyList<ArtisanProfile>> GetArtisansAsync(string? categorySlug, CancellationToken ct);

    /// <summary>A single artisan profile by id, or null if not found.</summary>
    Task<ArtisanProfile?> GetArtisanByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The artisan profile linked to a login account, or null if this
    /// user has no artisan profile. Used to resolve a signed-in artisan to the
    /// jobs assigned to their profile.</summary>
    Task<ArtisanProfile?> GetArtisanByUserIdAsync(Guid userId, CancellationToken ct);
}
