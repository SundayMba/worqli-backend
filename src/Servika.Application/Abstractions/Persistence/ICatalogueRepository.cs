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

    /// <summary>Artisan summaries, optionally filtered to one category slug.</summary>
    Task<IReadOnlyList<ArtisanProfile>> GetArtisansAsync(string? categorySlug, CancellationToken ct);

    /// <summary>A single artisan profile by id, or null if not found.</summary>
    Task<ArtisanProfile?> GetArtisanByIdAsync(Guid id, CancellationToken ct);
}
