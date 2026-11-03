using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>The artisan's live offers (GET /artisan/jobs/offers): one job each, with its countdown.</summary>
public sealed class GetMyOffersHandler
{
    private readonly IJobOfferRepository _offers;
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly ArtisanBookingDtoBuilder _dto;
    private readonly IClock _clock;

    public GetMyOffersHandler(IJobOfferRepository offers, IBookingRepository bookings, ICatalogueRepository catalogue,
        ArtisanBookingDtoBuilder dto, IClock clock)
    {
        _offers = offers;
        _bookings = bookings;
        _catalogue = catalogue;
        _dto = dto;
        _clock = clock;
    }

    public async Task<IReadOnlyList<JobOfferDto>> ListAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (profile is null) return Array.Empty<JobOfferDto>();
        var list = new List<JobOfferDto>();
        foreach (var offer in await _offers.ListLiveForArtisanAsync(profile.Id, _clock.UtcNow, ct))
        {
            var booking = await _bookings.FindByIdReadOnlyAsync(offer.BookingId, ct);
            if (booking is null || booking.Status is not (BookingStatus.Open or BookingStatus.Pending)) continue;
            list.Add(await ToDtoAsync(offer, booking, ct));
        }
        return list;
    }

    public async Task<JobOfferDto> GetAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("This offer was not found.");
        var offer = await _offers.FindLiveForArtisanAsync(bookingId, profile.Id, ct);
        if (offer is null || !offer.IsLive(_clock.UtcNow))
            throw new NotFoundException("This offer is no longer open.");
        var booking = await _bookings.FindByIdReadOnlyAsync(bookingId, ct)
            ?? throw new NotFoundException("This offer was not found.");
        return await ToDtoAsync(offer, booking, ct);
    }

    private async Task<JobOfferDto> ToDtoAsync(JobOffer offer, Booking booking, CancellationToken ct) =>
        new(offer.Id, offer.BookingId, offer.Round, offer.Source.ToString(), offer.OfferedAtUtc, offer.ExpiresAtUtc,
            await _dto.BuildAsync(booking, 0, ct));
}

/// <summary>
/// The artisan answers an offer. Accept assigns the job (atomically for rotation, so a stale
/// offer can never double-assign); decline passes it to the next artisan at once.
/// </summary>
public sealed class RespondToOfferHandler
{
    private readonly IJobOfferRepository _offers;
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly AssignmentService _assignment;
    private readonly NotificationEmitter _notifications;
    private readonly ArtisanBookingDtoBuilder _dto;
    private readonly IClock _clock;

    public RespondToOfferHandler(IJobOfferRepository offers, IBookingRepository bookings, ICatalogueRepository catalogue,
        AssignmentService assignment, NotificationEmitter notifications, ArtisanBookingDtoBuilder dto, IClock clock)
    {
        _offers = offers;
        _bookings = bookings;
        _catalogue = catalogue;
        _assignment = assignment;
        _notifications = notifications;
        _dto = dto;
        _clock = clock;
    }

    public async Task<BookingDetailDto> AcceptAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        if (profile.VerificationStatus != ArtisanVerificationStatus.Verified)
            throw new ConflictException("Your profile must be verified before you can take jobs.");
        var offer = await _offers.FindLiveForArtisanAsync(bookingId, profile.Id, ct);
        if (offer is null || !offer.IsLive(now))
            throw new ConflictException("This offer is no longer open. It went to the next artisan.");

        Booking assigned;
        if (offer.Source == JobOfferSource.Direct)
        {
            var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
                ?? throw new NotFoundException("This job was not found.");
            booking.Accept(now);
            await _notifications.BookingAdvancedByArtisanAsync(booking, ArtisanBookingAction.Accept, 0, ct);
            assigned = booking;
        }
        else
        {
            // One guarded UPDATE: only a still-Open request flips to Accepted, so two stale
            // offers can never both win.
            var won = await _bookings.TryClaimAsync(bookingId, profile.Id, profile.FullName, now, ct);
            if (!won)
            {
                offer.Withdraw(now);
                await _offers.SaveChangesAsync(ct);
                throw new ConflictException("This job was just taken or cancelled.");
            }
            assigned = await _bookings.FindByIdReadOnlyAsync(bookingId, ct)
                ?? throw new NotFoundException("This job was not found.");
            _notifications.OpenJobClaimed(assigned);
        }

        offer.Accept(now);
        profile.MarkAssigned(now);
        await _offers.SaveChangesAsync(ct);
        return await _dto.BuildAsync(assigned, 0, ct);
    }

    public async Task DeclineAsync(Guid artisanUserId, Guid bookingId, string? reason, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        var offer = await _offers.FindLiveForArtisanAsync(bookingId, profile.Id, ct);
        if (offer is null || !offer.IsLive(now))
            throw new ConflictException("This offer is no longer open.");
        var booking = await _bookings.FindByIdAsync(bookingId, ct)
            ?? throw new NotFoundException("This job was not found.");

        offer.Decline(now, reason);
        if (offer.Source == JobOfferSource.Direct && booking.Status == BookingStatus.Pending)
        {
            booking.Reject();
            await _notifications.BookingAdvancedByArtisanAsync(booking, ArtisanBookingAction.Reject, 0, ct);
        }
        await _assignment.ReassignAsync(booking, profile.Id, reason, now, ct);
        await _offers.SaveChangesAsync(ct);
    }
}

/// <summary>
/// The sweep behind the offer window: expires offers nobody answered and moves the job to the
/// next artisan; every <see cref="AssignmentService.RetryExhaustedAfterMinutes"/> it retries
/// requests that found nobody, in case someone came online.
/// </summary>
public sealed class OfferSweepHandler
{
    private readonly IJobOfferRepository _offers;
    private readonly IBookingRepository _bookings;
    private readonly AssignmentService _assignment;
    private readonly IClock _clock;

    public OfferSweepHandler(IJobOfferRepository offers, IBookingRepository bookings, AssignmentService assignment, IClock clock)
    {
        _offers = offers;
        _bookings = bookings;
        _assignment = assignment;
        _clock = clock;
    }

    /// <summary>A price-book booking with no payment started lapses after this long.</summary>
    public const int UnpaidUpfrontLapseHours = 2;
    /// <summary>One whose payment is still pending (a slow bank transfer) gets a day.</summary>
    public const int PendingUpfrontLapseHours = 24;

    /// <summary>Returns (expired offers, retried requests).</summary>
    public async Task<(int Expired, int Retried)> RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var expired = await _offers.ListExpiredLiveAsync(now, ct);
        foreach (var offer in expired)
        {
            offer.Expire(now);
            var booking = await _bookings.FindByIdAsync(offer.BookingId, ct);
            if (booking is null) continue;
            if (offer.Source == JobOfferSource.Direct && booking.Status == BookingStatus.Pending)
                await _assignment.ReassignAsync(booking, null, null, now, ct);
            else if (booking.Status == BookingStatus.Open)
                await _assignment.OfferNextAsync(booking, now, ct);
        }

        var retry = await _bookings.ListOpenExhaustedBeforeAsync(now.AddMinutes(-AssignmentService.RetryExhaustedAfterMinutes), ct);
        foreach (var booking in retry) await _assignment.OfferNextAsync(booking, now, ct);

        // A price-book job nobody paid for lapses, so it never lingers as a live booking.
        var lapsed = await _bookings.ListLapsedUpfrontAsync(
            now.AddHours(-UnpaidUpfrontLapseHours), now.AddHours(-PendingUpfrontLapseHours), ct);
        foreach (var booking in lapsed) booking.ExpireUnpaid();

        if (expired.Count > 0 || retry.Count > 0 || lapsed.Count > 0) await _offers.SaveChangesAsync(ct);
        return (expired.Count, retry.Count);
    }
}
