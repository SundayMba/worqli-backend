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
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public SubmitBidHandler(
        IBookingRepository bookings, IBidRepository bids, ICatalogueRepository catalogue,
        NotificationEmitter notifications, Payments.ArtisanStandingService standing, IClock clock,
        IPlatformSettingsRepository settings)
    {
        _settings = settings;
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

        // A fixed-price booking IS the price — no quotes, no bargaining. The
        // artisan accepts or declines it as published.
        if (booking.PricingModel == PricingModel.Fixed)
            throw new ConflictException(
                "This is a fixed-price booking. Accept or decline it. The price is already set.");
        // A diagnosis visit is priced from Servika's list: the artisan picks the repairs, never writes a price.
        if (booking.IsDiagnosis && !booking.IsCustomQuote)
            throw new ConflictException(
                "This is a diagnosis visit. Pick the repairs it needs from Servika's list instead of sending a quote.");

        // The assigned artisan can quote on their own direct request before the
        // customer accepts (Pending) — and also en-route/on-site (Accepted →
        // Arrived), which is how an inspect-first visit produces its in-app
        // quote. A direct request is only visible to its pre-selected artisan —
        // anyone else probing one sees a 404, never a hint it exists.
        var isDirectQuote = booking.ArtisanId == profile.Id
            && booking.Status is BookingStatus.Pending or BookingStatus.Accepted
                or BookingStatus.OnMyWay or BookingStatus.Arrived;
        if (isDirectQuote && booking.IsCustomQuote)
        {
            // A custom job is quoted after the paid visit, once, before the customer accepts.
            if (booking.Status != BookingStatus.Arrived)
                throw new ConflictException("Quote once you have seen the job.");
            if (booking.HasAcceptedCustomQuote)
                throw new ConflictException("The customer already accepted your quote, so the price can't change.");
        }
        else if (isDirectQuote)
        {
            if (booking.PaymentState == BookingPaymentState.Paid)
                throw new ConflictException("This job is already paid, so the price can't change.");
        }
        else
        {
            // Open bidding (inDrive-style): any verified artisan in the trade whose work rules accept the job may
            // bid. A request assigned to someone else stays invisible.
            if (booking.ArtisanId is not null)
                throw new NotFoundException("This request was not found.");
            if (booking.Status != BookingStatus.Open || booking.Assessment != AssessmentMode.RemoteQuote)
                throw new ConflictException("This job is no longer taking bids.");
            if (!profile.CategorySlugs.Contains(booking.CategorySlug))
                throw new ConflictException("This job is not in your trades.");
            if (await _standing.IsRestrictedAsync(profile.Id, ct))
                throw new ConflictException("Settle your service fees to bid on new jobs.");
            if (OpenJobMatching.RefusalReason(profile, booking, _clock.UtcNow) is { } refusal)
                throw new ConflictException(refusal);
        }
        if (request.VisitFirst && isDirectQuote)
            throw new ArgumentException("This job was sent to you. Accept it to visit first, or send your price.");

        var now = _clock.UtcNow;
        // Itemised quote: labour + material lines (total derived). A legacy
        // single-price body is all-workmanship.
        var materials = (request.Materials ?? Array.Empty<BidMaterialLineDto>())
            .Select(m => BidMaterialLine.Create(m.Name, m.Quantity, m.UnitPriceNaira))
            .ToList();
        var workmanship = request.WorkmanshipNaira ?? request.AmountNaira;

        var existing = await _bids.FindForArtisanAsync(bookingId, profile.Id, ct);
        Bid bid;
        var wasVisible = existing?.IsVisibleToCustomer ?? false;
        // The artisan who was picked to visit first now prices the job: that quote is news to the customer.
        var pricingAfterVisit = existing?.Kind == BidKind.VisitFirst && !request.VisitFirst && isDirectQuote;
        if (request.VisitFirst)
        {
            if (existing is not null)
            {
                existing.ReviseToVisit(request.VisitFeeNaira, request.MaterialsNote, now);
                bid = existing;
            }
            else
            {
                bid = Bid.PlaceVisit(bookingId, profile.Id, artisanUserId, profile.FullName, request.VisitFeeNaira, request.MaterialsNote, now);
                _bids.Add(bid);
            }
        }
        else if (existing is not null)
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
        }

        if (booking.IsCustomQuote && booking.CustomPriceBookItemId is not null)
        {
            // A job booked from a listed kind of bigger work (Business Model v1.4, section 10): ops check a large
            // quote, or labour far above the typical range, before the customer sees it.
            var settings = await _settings.GetOrCreateAsync(ct);
            var reasons = QuoteReviewRules.Reasons(booking, bid, settings);
            bid.SetReviewNeeded(reasons.Count > 0);
            if (reasons.Count > 0)
                await _notifications.QuoteNeedsReviewAsync(booking, bid, reasons, ct);
            else if (!wasVisible || existing is null)
                _notifications.BidPlaced(booking, bid);
        }
        else if (existing is null || pricingAfterVisit)
        {
            // A fresh bid, or the price after a visit, notifies; a tweak shouldn't ping the customer again.
            _notifications.BidPlaced(booking, bid);
        }

        await _bids.SaveChangesAsync(ct);
        return bid.ToDto(profile, forArtisan: true);
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
        return bid.ToDto(profile, forArtisan: true);
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

        // A custom quote waiting for Servika's check (or sent back) is not the customer's to see yet.
        var bids = (await _bids.ListForBookingAsync(bookingId, ct)).Where(b => b.IsVisibleToCustomer).ToList();
        var result = new List<BidDto>(bids.Count);
        var jobsDone = await _bookings.CountCompletedForArtisansAsync(bids.Select(b => b.ArtisanId).Distinct().ToList(), ct);
        foreach (var bid in bids)
        {
            // Bids are few per request — per-bid profile lookups are fine and keep the trust data (jobs done,
            // certificate, photo) live. No rating: ratings are for ops only.
            var profile = await _catalogue.GetArtisanByIdAsync(bid.ArtisanId, ct);
            result.Add(bid.ToDto(profile, DistanceToJobKm(booking, profile), completedJobs: jobsDone.GetValueOrDefault(bid.ArtisanId)));
        }
        return result;
    }

    /// <summary>Km from the job's location to the artisan's base pin, when both
    /// are known — powers the customer's "Nearest" sort. Null otherwise.</summary>
    internal static double? DistanceToJobKm(Booking booking, ArtisanProfile? profile) =>
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
        var winner = bids.FirstOrDefault(b => b.Id == bidId && b.IsVisibleToCustomer)
            ?? throw new NotFoundException("That bid was not found.");

        var now = _clock.UtcNow;
        var statusBeforeAccept = booking.Status;
        if (winner.Kind == BidKind.VisitFirst)
        {
            // "Visit first": the artisan is assigned; the visit fee (if any) is paid now and credited against the
            // price they send after seeing the job. Their bid stays open so they can turn it into that price.
            if (statusBeforeAccept != BookingStatus.Open)
                throw new ConflictException("An offer to visit can only be picked on an open request.");
            booking.AcceptVisitBid(winner.ArtisanId, winner.ArtisanName, winner.VisitFeeNaira, now);
            foreach (var other in bids.Where(b => b.Id != winner.Id)) other.MarkClosed(now);
        }
        else
        {
            SettleOnBooking(booking, winner, bids, now);
        }

        _notifications.BidAccepted(booking, winner, statusBeforeAccept);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto(0, null, null);
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
        // A custom quote is checked against the typical range and by ops; it is accepted or declined, not haggled.
        if (booking.IsCustomQuote)
            throw new ConflictException("Custom quotes are checked by Servika. Accept it, or decline and pay only the visit.");
        if (booking.PaymentState == BookingPaymentState.Paid)
            throw new ConflictException("This job is already paid, so the price can't change.");

        var bids = await _bids.ListForBookingAsync(bookingId, ct);
        var bid = bids.FirstOrDefault(b => b.Id == bidId)
            ?? throw new NotFoundException("That offer was not found.");

        bid.Counter(request.WorkmanshipNaira, request.Note, _clock.UtcNow);
        _notifications.CounterOfferMade(booking, bid);
        await _bids.SaveChangesAsync(ct);

        var profile = await _catalogue.GetArtisanByIdAsync(bid.ArtisanId, ct);
        return bid.ToDto(profile, GetBookingBidsHandler.DistanceToJobKm(booking, profile));
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
    private readonly ArtisanBookingDtoBuilder _dto;
    private readonly IClock _clock;

    public RespondToCounterHandler(
        IBookingRepository bookings, IBidRepository bids, ICatalogueRepository catalogue,
        NotificationEmitter notifications, ArtisanBookingDtoBuilder dto, IClock clock)
    {
        _dto = dto;
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
        return await _dto.BuildAsync(booking, 0, ct);
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
    public static BidDto ToDto(this Bid bid, ArtisanProfile? profile, double? distanceKm = null, bool forArtisan = false, int completedJobs = 0) =>
        new(
            bid.Id,
            bid.BookingId,
            bid.ArtisanId,
            bid.ArtisanName,
            completedJobs,
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
            Bid.MaxCounterRounds,
            bid.Review.ToString(),
            forArtisan ? bid.ReviewNote : null,
            bid.Kind.ToString(),
            bid.VisitFeeNaira);
}
