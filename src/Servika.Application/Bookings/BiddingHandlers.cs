using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// An artisan places (or revises) a price offer. Two cases: a competing bid on
/// an open RemoteQuote broadcast (verified profile, category match, request
/// still Open, bidding mode), or the pre-selected artisan quoting on their own
/// direct Pending request (they were chosen by name — no category gate; the
/// quote is how a direct job gets its agreed price before any payment).
/// </summary>
public sealed class SubmitBidHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;
    private readonly ICatalogueRepository _catalogue;
    private readonly NotificationEmitter _notifications;
    private readonly Payments.ArtisanStandingService _standing;
    private readonly IClock _clock;

    public SubmitBidHandler(
        IBookingRepository bookings, IBidRepository bids, ICatalogueRepository catalogue,
        NotificationEmitter notifications, Payments.ArtisanStandingService standing, IClock clock)
    {
        _bookings = bookings;
        _bids = bids;
        _catalogue = catalogue;
        _notifications = notifications;
        _standing = standing;
        _clock = clock;
    }

    public async Task<BidDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, SubmitBidRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new ConflictException("Set up and verify your Pro profile to bid.");
        if (profile.VerificationStatus != ArtisanVerificationStatus.Verified)
            throw new ConflictException("Your profile must be verified before you can bid.");

        var booking = await _bookings.FindByIdReadOnlyAsync(bookingId, ct)
            ?? throw new NotFoundException("This request was not found.");

        // The assigned artisan can quote on their own direct request before the
        // customer accepts (Pending) — and also en-route/on-site (Accepted →
        // Arrived), which is how an inspect-first visit produces its in-app
        // quote. A direct request is only visible to its pre-selected artisan —
        // anyone else probing one sees a 404, never a hint it exists.
        var isDirectQuote = booking.ArtisanId == profile.Id
            && booking.Status is BookingStatus.Pending or BookingStatus.Accepted
                or BookingStatus.OnMyWay or BookingStatus.Arrived;
        if (isDirectQuote)
        {
            if (booking.PaymentState == BookingPaymentState.Paid)
                throw new ConflictException("This job is already paid, so the price can't change.");
        }
        else
        {
            if (booking.Status == BookingStatus.Pending)
                throw new NotFoundException("This request was not found.");
            if (booking.Status != BookingStatus.Open)
                throw new ConflictException("This request is no longer open.");
            if (booking.Assessment != AssessmentMode.RemoteQuote)
                throw new ConflictException(
                    "This request is inspect-first. Accept it directly instead of bidding.");
            if (!profile.CategorySlugs.Contains(booking.CategorySlug))
                throw new ConflictException("This job is not in your service categories.");
            // Standing gate applies to competing for NEW work only — an artisan
            // quoting on a job already assigned to them is never blocked (online
            // jobs are how the debt auto-nets down).
            if (await _standing.IsRestrictedAsync(profile.Id, ct))
                throw new ConflictException(
                    "Settle your outstanding Servika service fees to bid on new jobs.");
        }

        var now = _clock.UtcNow;
        // Itemised quote: labour + material lines (total derived). A legacy
        // single-price body is all-workmanship.
        var materials = (request.Materials ?? Array.Empty<BidMaterialLineDto>())
            .Select(m => BidMaterialLine.Create(m.Name, m.Quantity, m.UnitPriceNaira))
            .ToList();
        var workmanship = request.WorkmanshipNaira ?? request.AmountNaira;

        var existing = await _bids.FindForArtisanAsync(bookingId, profile.Id, ct);
        Bid bid;
        if (existing is not null)
        {
            var answeringCounter = existing.HasPendingCounter;
            existing.Revise(workmanship, materials, request.MaterialsNote, now);
            bid = existing;
            // A revised price that answers the customer's counter IS news to them.
            if (answeringCounter) _notifications.BidRevisedAfterCounter(booking, bid);
        }
        else
        {
            bid = Bid.Place(
                bookingId, profile.Id, artisanUserId, profile.FullName,
                workmanship, materials, request.MaterialsNote, now);
            _bids.Add(bid);
            // Only a fresh bid notifies — a price tweak shouldn't ping the customer again.
            _notifications.BidPlaced(booking, bid);
        }

        await _bids.SaveChangesAsync(ct);
        return bid.ToDto(profile);
    }
}

/// <summary>The artisan's own bid on a request (404 = not bid yet).</summary>
public sealed class GetMyBidHandler
{
    private readonly IBidRepository _bids;
    private readonly ICatalogueRepository _catalogue;

    public GetMyBidHandler(IBidRepository bids, ICatalogueRepository catalogue)
    {
        _bids = bids;
        _catalogue = catalogue;
    }

    public async Task<BidDto> HandleAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No Pro profile.");
        var bid = await _bids.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException("You haven't bid on this request.");
        return bid.ToDto(profile);
    }
}

/// <summary>The customer reviews the bids on their open request.</summary>
public sealed class GetBookingBidsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;
    private readonly ICatalogueRepository _catalogue;

    public GetBookingBidsHandler(
        IBookingRepository bookings, IBidRepository bids, ICatalogueRepository catalogue)
    {
        _bookings = bookings;
        _bids = bids;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<BidDto>> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("Booking was not found.");

        var bids = await _bids.ListForBookingAsync(bookingId, ct);
        var result = new List<BidDto>(bids.Count);
        foreach (var bid in bids)
        {
            // Bids are few per request — per-bid profile lookups are fine and keep
            // the reputation data (rating, certificate, photo) live, not stale.
            var profile = await _catalogue.GetArtisanByIdAsync(bid.ArtisanId, ct);
            result.Add(bid.ToDto(profile, DistanceToJobKm(booking, profile)));
        }
        return result;
    }

    /// <summary>Km from the job's location to the artisan's base pin, when both
    /// are known — powers the customer's "Nearest" sort. Null otherwise.</summary>
    private static double? DistanceToJobKm(Booking booking, ArtisanProfile? profile) =>
        booking is { LocationLat: { } jobLat, LocationLng: { } jobLng } &&
        profile is { Latitude: { } artLat, Longitude: { } artLng }
            ? Math.Round(GeoDistance.Km(jobLat, jobLng, artLat, artLng), 1)
            : null;
}

/// <summary>
/// The customer accepts one bid: the booking is assigned to that artisan at the
/// offered price (Open → Accepted), every other bid closes, the winner is told.
/// </summary>
public sealed class AcceptBidHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public AcceptBidHandler(
        IBookingRepository bookings, IBidRepository bids,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _bids = bids;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, Guid bidId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("Booking was not found.");

        var bids = await _bids.ListForBookingAsync(bookingId, ct);
        var winner = bids.FirstOrDefault(b => b.Id == bidId)
            ?? throw new NotFoundException("That bid was not found.");

        var now = _clock.UtcNow;
        var statusBeforeAccept = booking.Status;
        SettleOnBooking(booking, winner, bids, now);

        _notifications.BidAccepted(booking, winner, statusBeforeAccept);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto(bidCount: 0);
    }

    /// <summary>Makes <paramref name="winner"/> the booking's agreed price and closes
    /// every other bid. Shared with the counter-offer acceptance path.</summary>
    internal static void SettleOnBooking(
        Booking booking, Bid winner, IReadOnlyList<Bid> bids, DateTimeOffset now)
    {
        booking.AcceptBid(
            winner.ArtisanId, winner.ArtisanName, winner.AmountNaira, now,
            winner.WorkmanshipNaira, winner.MaterialsNaira);
        winner.MarkAccepted(now);
        foreach (var other in bids.Where(b => b.Id != winner.Id))
            other.MarkClosed(now);
    }
}

/// <summary>
/// The customer counters an offer's WORKMANSHIP price (inDrive-style bargaining).
/// Nothing is agreed yet: the artisan accepts (deal), declines, or sends a new price.
/// Capped at <see cref="Bid.MaxCounterRounds"/> rounds so it ends.
/// </summary>
public sealed class CounterBidHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;
    private readonly ICatalogueRepository _catalogue;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public CounterBidHandler(
        IBookingRepository bookings, IBidRepository bids, ICatalogueRepository catalogue,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _bids = bids;
        _catalogue = catalogue;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BidDto> HandleAsync(
        Guid customerId, Guid bookingId, Guid bidId, CounterBidRequest request, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("Booking was not found.");
        if (booking.PaymentState == BookingPaymentState.Paid)
            throw new ConflictException("This job is already paid, so the price can't change.");

        var bids = await _bids.ListForBookingAsync(bookingId, ct);
        var bid = bids.FirstOrDefault(b => b.Id == bidId)
            ?? throw new NotFoundException("That offer was not found.");

        bid.Counter(request.WorkmanshipNaira, request.Note, _clock.UtcNow);
        _notifications.CounterOfferMade(booking, bid);
        await _bids.SaveChangesAsync(ct);

        var profile = await _catalogue.GetArtisanByIdAsync(bid.ArtisanId, ct);
        return bid.ToDto(profile);
    }
}

/// <summary>
/// The artisan answers the customer's counter-offer on their own bid: accept (the
/// price is agreed at the customer's number and the booking proceeds exactly as if
/// the customer had accepted the quote) or decline (the artisan's quote stands).
/// </summary>
public sealed class RespondToCounterHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;
    private readonly ICatalogueRepository _catalogue;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public RespondToCounterHandler(
        IBookingRepository bookings, IBidRepository bids, ICatalogueRepository catalogue,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _bids = bids;
        _catalogue = catalogue;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BidDto> DeclineAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var (profile, bid, booking) = await LoadAsync(artisanUserId, bookingId, ct);
        var declined = bid.PendingCounterNaira ?? 0;
        bid.DeclineCounter(_clock.UtcNow);
        _notifications.CounterOfferDeclined(booking, bid, declined);
        await _bids.SaveChangesAsync(ct);
        return bid.ToDto(profile);
    }

    public async Task<BookingDetailDto> AcceptAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var (_, bid, booking) = await LoadAsync(artisanUserId, bookingId, ct);
        var now = _clock.UtcNow;
        var statusBefore = booking.Status;

        bid.AcceptCounter(now);
        var bids = await _bids.ListForBookingAsync(bookingId, ct);
        AcceptBidHandler.SettleOnBooking(booking, bid, bids, now);

        _notifications.CounterOfferAccepted(booking, bid, statusBefore);
        await _bookings.SaveChangesAsync(ct);
        return booking.ToDetailDto(bidCount: 0);
    }

    private async Task<(ArtisanProfile profile, Bid bid, Booking booking)> LoadAsync(
        Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No Pro profile.");
        var bid = await _bids.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException("You haven't quoted on this request.");
        // Tracked booking: accepting mutates it. Open (broadcast) or the artisan's own direct job.
        var booking = await _bookings.FindByIdAsync(bookingId, ct)
            ?? throw new NotFoundException("This request was not found.");
        return (profile, bid, booking);
    }
}

/// <summary>
/// Serves the customer's job photos / video clip. Visible to the booking's
/// owner, the assigned artisan, and — while the request is open — any verified
/// artisan in its category (they need the context to bid). Everyone else: 404.
/// </summary>
public sealed class GetBookingMediaHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public GetBookingMediaHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue, IFileStorage files)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _files = files;
    }

    public async Task<StoredFile> HandleAsync(
        Guid callerUserId, Guid bookingId, string mediaKey, CancellationToken ct)
    {
        var booking = await _bookings.FindByIdReadOnlyAsync(bookingId, ct)
            ?? throw new NotFoundException("Booking was not found.");
        if (!booking.MediaKeys.Contains(mediaKey) && booking.VideoKey != mediaKey)
            throw new NotFoundException("No such media on this booking.");

        if (booking.CustomerId != callerUserId)
        {
            var profile = await _catalogue.GetArtisanByUserIdAsync(callerUserId, ct);
            var isAssigned = profile is not null && booking.ArtisanId == profile.Id;
            var isEligibleBidder =
                profile is not null &&
                profile.VerificationStatus == ArtisanVerificationStatus.Verified &&
                booking.Status == BookingStatus.Open &&
                profile.CategorySlugs.Contains(booking.CategorySlug);
            if (!isAssigned && !isEligibleBidder)
                throw new NotFoundException("Booking was not found.");
        }

        return await _files.GetAsync(mediaKey, ct)
            ?? throw new NotFoundException("The media file was not found.");
    }
}

internal static class BidMapping
{
    public static BidDto ToDto(this Bid bid, ArtisanProfile? profile, double? distanceKm = null) =>
        new(
            bid.Id,
            bid.BookingId,
            bid.ArtisanId,
            bid.ArtisanName,
            profile?.Rating ?? 0,
            profile?.ReviewCount ?? 0,
            profile?.HasCertificate ?? false,
            profile is { PhotoKey: not null and not "" }
                ? $"/api/v1/artisans/{profile.Id}/photo"
                : null,
            bid.AmountNaira,
            bid.MaterialsNote,
            bid.Status.ToString(),
            bid.CreatedAt,
            distanceKm,
            bid.WorkmanshipNaira,
            bid.MaterialsNaira,
            bid.Materials.Select(m => new BidMaterialLineDto(m.Name, m.Quantity, m.UnitPriceNaira)).ToList(),
            bid.PendingCounterNaira,
            bid.PendingCounterNote,
            bid.CounterRounds,
            Bid.MaxCounterRounds);
}
