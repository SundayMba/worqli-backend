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
    private readonly Payments.ArtisanStandingService _standing;

    public GetOpenJobsHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        Payments.ArtisanStandingService standing)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _standing = standing;
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> HandleAsync(
        Guid artisanUserId, CancellationToken ct)
    {
        // A pending artisan may READ the pool (real jobs, real prices, actions locked
        // in the app); claiming/quoting still requires Verified server-side.
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (profile is null || profile.VerificationStatus == ArtisanVerificationStatus.Rejected)
            return Array.Empty<BookingSummaryDto>();

        // Past the commission-debt limit → no new requests until settled (the
        // app explains why via the wallet's IsRestricted flag).
        if (await _standing.IsRestrictedAsync(profile.Id, ct))
            return Array.Empty<BookingSummaryDto>();

        var open = await _bookings.ListOpenInCategoriesAsync(profile.CategorySlugs, ct);
        return open.Select(b => b.ToSummaryDto()).ToList();
    }
}
