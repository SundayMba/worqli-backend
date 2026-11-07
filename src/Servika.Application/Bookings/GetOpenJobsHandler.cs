using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Bookings;
using Servika.Domain.Catalogue;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// The open jobs a signed-in artisan can bid on: in their trades and within reach of their base (see
/// <see cref="OpenJobMatching.PoolAccepts"/>), nearest first, then newest. Unpinned jobs come last.
/// A pending artisan may read the pool (bidding needs Verified server-side); a restricted or away one sees none.
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
            .Where(b => OpenJobMatching.PoolAccepts(rules, b, now))
            .Select(b => (Booking: b, Km: OpenJobMatching.DistanceKm(rules, b)))
            .OrderBy(x => x.Km is null)
            .ThenBy(x => x.Km ?? 0)
            .ThenByDescending(x => x.Booking.CreatedAt)
            .Select(x => OpenJobPrivacy.Coarsen(x.Booking.ToSummaryDto(artisanSide: true)))
            .ToList();
    }
}

/// <summary>One open job in full, for an artisan deciding what to bid. 404 unless it is open and in their trades.</summary>
public sealed class GetOpenJobDetailHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IBidRepository _bids;
    private readonly ArtisanBookingDtoBuilder _dto;

    public GetOpenJobDetailHandler(IBookingRepository bookings, ICatalogueRepository catalogue, IBidRepository bids, ArtisanBookingDtoBuilder dto)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _bids = bids;
        _dto = dto;
    }

    public async Task<BookingDetailDto> HandleAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (profile is null || profile.VerificationStatus == ArtisanVerificationStatus.Rejected)
            throw new Common.NotFoundException("This request was not found.");
        var booking = await _bookings.FindByIdReadOnlyAsync(bookingId, ct);
        if (booking is null || booking.Status != Domain.Bookings.BookingStatus.Open || !profile.CategorySlugs.Contains(booking.CategorySlug))
            throw new Common.NotFoundException("This request was not found.");
        var dto = await _dto.BuildAsync(booking, await _bids.CountActiveForBookingAsync(booking.Id, ct), ct);
        return OpenJobPrivacy.Coarsen(dto, profile.VerificationStatus == ArtisanVerificationStatus.Verified);
    }
}
