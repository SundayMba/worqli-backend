using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ICatalogueRepository"/>. Read-only
/// queries (no tracking) — the catalogue is reference data the customer app
/// only reads. Category filtering uses a Postgres array containment check on the
/// artisan's <c>CategorySlugs</c> column.
/// </summary>
public sealed class CatalogueRepository : ICatalogueRepository
{
    private readonly ServikaDbContext _db;

    public CatalogueRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceCategory>> GetCategoriesAsync(CancellationToken ct) =>
        await _db.ServiceCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public Task<bool> CategoryExistsAsync(string slug, CancellationToken ct) =>
        _db.ServiceCategories.AnyAsync(c => c.IsActive && c.Slug == slug, ct);

    public async Task<IReadOnlyList<ArtisanProfile>> GetArtisansAsync(
        string? categorySlug, CancellationToken ct)
    {
        var query = _db.ArtisanProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            // Translates to `category_slugs @> ARRAY[@slug]` in Postgres.
            query = query.Where(a => a.CategorySlugs.Contains(categorySlug));
        }

        return await query
            .OrderByDescending(a => a.IsAvailable)
            .ThenBy(a => a.DistanceKm)
            .ToListAsync(ct);
    }

    public Task<ArtisanProfile?> GetArtisanByIdAsync(Guid id, CancellationToken ct) =>
        _db.ArtisanProfiles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
}
