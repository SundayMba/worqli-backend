using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Reviews;

namespace Servika.Application.Reviews;

/// <summary>
/// Lists an artisan's reviews (newest first) for their public profile. Open to
/// guests, like the rest of the catalogue. Validates the artisan exists so an
/// unknown id is a clean 404 (consistent with the artisan-profile endpoint)
/// rather than a silently empty list.
/// </summary>
public sealed class GetArtisanReviewsHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IReviewRepository _reviews;

    public GetArtisanReviewsHandler(ICatalogueRepository catalogue, IReviewRepository reviews)
    {
        _catalogue = catalogue;
        _reviews = reviews;
    }

    public async Task<IReadOnlyList<ReviewDto>> HandleAsync(Guid artisanId, CancellationToken ct)
    {
        _ = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException($"Artisan '{artisanId}' was not found.");

        var reviews = await _reviews.ListForArtisanAsync(artisanId, ct);
        return reviews.Select(r => r.ToDto()).ToList();
    }
}
