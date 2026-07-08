using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// Lists the open (unassigned) requests a signed-in artisan can claim — the ones in
/// their service categories, newest first. An account with no profile, or one that
/// isn't verified yet, sees an empty pool (only verified artisans take jobs).
/// </summary>
public sealed class GetOpenJobsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;

    public GetOpenJobsHandler(IBookingRepository bookings, ICatalogueRepository catalogue)
    {
        _bookings = bookings;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> HandleAsync(
        Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (profile is null || profile.VerificationStatus != ArtisanVerificationStatus.Verified)
            return Array.Empty<BookingSummaryDto>();

        var open = await _bookings.ListOpenInCategoriesAsync(profile.CategorySlugs, ct);
        return open.Select(b => b.ToSummaryDto()).ToList();
    }
}
