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
    private readonly Abstractions.Time.IClock _clock;

    public GetOpenJobsHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        Payments.ArtisanStandingService standing,
        Abstractions.Time.IClock clock)
    {
        _clock = clock;
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

        // Away → no new requests until they're back; radius and emergency
        // preferences filter the rest (same rules as the broadcast).
        var now = _clock.UtcNow;
        if (OpenJobMatching.IsAway(profile.AwayUntilUtc, now))
            return Array.Empty<BookingSummaryDto>();
        var rules = OpenJobMatching.RulesOf(profile);
        var open = await _bookings.ListOpenInCategoriesAsync(profile.CategorySlugs, ct);
        return open
            .Where(b => OpenJobMatching.Accepts(rules, b, now))
            .Select(b => b.ToSummaryDto(artisanSide: true))
            .ToList();
    }
}
