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
    private readonly IBookingRepository _bookings;

    public GetArtisansHandler(ICatalogueRepository catalogue, IBookingRepository bookings)
    {
        _catalogue = catalogue;
        _bookings = bookings;
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
        var completed = await _bookings.CountCompletedForArtisansAsync(artisans.Select(a => a.Id).ToList(), ct);

        // Distance only when both the customer's and the artisan's locations are
        // known; otherwise null (never a stored placeholder). Available first, then
        // the ranking score (rating + certificate boost), then proximity breaks ties.
        return artisans
            .Select(a => (Artisan: a, Distance: a.DistanceFrom(lat, lng)))
            .OrderByDescending(x => x.Artisan.IsAvailable)
            .ThenByDescending(x => x.Artisan.RankScore)
            .ThenBy(x => x.Distance ?? double.MaxValue)
            .Select(x => x.Artisan.ToSummaryDto(x.Distance, completed.GetValueOrDefault(x.Artisan.Id)))
            .ToList();
    }
}
