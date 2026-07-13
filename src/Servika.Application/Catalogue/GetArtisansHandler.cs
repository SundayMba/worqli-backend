using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// Lists artisan summaries for the home carousel (no filter) or for a single
/// category (when <paramref name="categorySlug"/> is supplied). An unknown
/// category slug is a 404 rather than a silent empty list.
///
/// When the customer's coordinates are supplied, each artisan's distance is
/// computed from those coordinates and the list is re-sorted by proximity
/// (available artisans first, then nearest). Without coordinates it falls back
/// to the artisans' seeded baseline distance and order.
/// </summary>
public sealed class GetArtisansHandler
{
    private readonly ICatalogueRepository _catalogue;

    public GetArtisansHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<ArtisanSummaryDto>> HandleAsync(
        string? categorySlug, double? lat, double? lng, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(categorySlug) &&
            !await _catalogue.CategoryExistsAsync(categorySlug, ct))
        {
            throw new NotFoundException($"Category '{categorySlug}' was not found.");
        }

        var artisans = await _catalogue.GetArtisansAsync(categorySlug, ct);

        // No customer location → rank by score (rating + certificate boost),
        // available artisans first.
        if (lat is not { } customerLat || lng is not { } customerLng)
        {
            return artisans
                .OrderByDescending(a => a.IsAvailable)
                .ThenByDescending(a => a.RankScore)
                .Select(a => a.ToSummaryDto())
                .ToList();
        }

        // Compute real distance for artisans that have coordinates; fall back to
        // the seeded baseline for any that don't, so none silently disappears.
        return artisans
            .Select(a =>
            {
                var distance = a.Latitude is { } aLat && a.Longitude is { } aLng
                    ? Math.Round(GeoDistance.Km(customerLat, customerLng, aLat, aLng), 1)
                    : a.DistanceKm;
                return (Artisan: a, Distance: distance);
            })
            // Available first, then the ranking score (rating + certificate
            // boost), then proximity breaks ties — quality leads, distance
            // still matters.
            .OrderByDescending(x => x.Artisan.IsAvailable)
            .ThenByDescending(x => x.Artisan.RankScore)
            .ThenBy(x => x.Distance)
            .Select(x => x.Artisan.ToSummaryDto(x.Distance))
            .ToList();
    }
}
