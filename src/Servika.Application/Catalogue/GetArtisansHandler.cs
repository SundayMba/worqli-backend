using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// Lists artisan summaries for the home carousel (no filter) or for a single
/// category (when <paramref name="categorySlug"/> is supplied). An unknown
/// category slug is a 404 rather than a silent empty list.
/// </summary>
public sealed class GetArtisansHandler
{
    private readonly ICatalogueRepository _catalogue;

    public GetArtisansHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<ArtisanSummaryDto>> HandleAsync(
        string? categorySlug, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(categorySlug) &&
            !await _catalogue.CategoryExistsAsync(categorySlug, ct))
        {
            throw new NotFoundException($"Category '{categorySlug}' was not found.");
        }

        var artisans = await _catalogue.GetArtisansAsync(categorySlug, ct);
        return artisans.Select(a => a.ToSummaryDto()).ToList();
    }
}
