using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Notifications;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// Servika assigns the job. A request is offered to one artisan at a time for a window
/// (<c>PlatformSettings.OfferWindowMinutes</c>); a decline or a timeout moves it to the next
/// artisan in rotation. When nobody is left the request stays open, the customer is told, ops
/// are alerted, and the sweep tries again later as artisans come online. Stages its writes on
/// the caller's DbContext; the caller saves.
/// </summary>
public sealed class AssignmentService
{
    /// <summary>How long an exhausted request waits before the sweep looks for newly online artisans.</summary>
    public const int RetryExhaustedAfterMinutes = 15;

    private readonly IJobOfferRepository _offers;
    private readonly ICatalogueRepository _catalogue;
    private readonly Payments.ArtisanStandingService _standing;
    private readonly IPlatformSettingsRepository _settings;
    private readonly NotificationEmitter _notifications;
    private readonly IAdminAlerts _alerts;

    public AssignmentService(
        IJobOfferRepository offers, ICatalogueRepository catalogue, Payments.ArtisanStandingService standing,
        IPlatformSettingsRepository settings, NotificationEmitter notifications, IAdminAlerts alerts)
    {
        _offers = offers;
        _catalogue = catalogue;
        _standing = standing;
        _settings = settings;
        _notifications = notifications;
        _alerts = alerts;
    }

    /// <summary>
    /// Puts an Open request out for bids (inDrive-style): every verified, online artisan in the trade whose work
    /// rules accept it (not away, inside their radius, emergencies only if they take them, not restricted for
    /// unpaid commission) is told at once and can send a price or "visit first". Nobody is picked by Servika;
    /// the customer picks from the bids. When nobody can take it the request stays open, the customer is told,
    /// ops are alerted and the sweep tries again as artisans come online. Returns how many were told.
    /// </summary>
    public async Task<int> BroadcastAsync(Booking booking, DateTimeOffset now, CancellationToken ct)
    {
        if (booking.Status != BookingStatus.Open) return 0;

        var recipients = await _catalogue.ListArtisanRecipientsInCategoryAsync(booking.CategorySlug, ct);
        var restricted = new HashSet<Guid>(await _standing.GetRestrictedProfileIdsAsync(ct));
        var eligible = recipients
            .Where(r => !restricted.Contains(r.ProfileId) && OpenJobMatching.Accepts(r.Rules, booking, now))
            .ToList();

        if (eligible.Count == 0)
        {
            var firstTime = booking.OffersExhaustedAtUtc is null;
            booking.MarkOffersExhausted(now);
            if (firstTime)
            {
                _notifications.NoArtisanFree(booking);
                _alerts.Send(new AdminAlert(AdminAlertKind.NoArtisanFree, "No artisan free for a request",
                    new[]
                    {
                        $"{booking.ServiceName} in {Area(booking)}, wanted {booking.PreferredDate:d MMM} {booking.PreferredTimeSlot}.",
                        $"No artisan in the trade could take it. It stays open and we try again every {RetryExhaustedAfterMinutes} minutes.",
                    },
                    $"/bookings/{booking.Id}", "Open booking"));
            }
            return 0;
        }

        foreach (var r in eligible) _notifications.OpenJobPosted(r.UserId, booking);
        booking.RecordOffer(now);
        return eligible.Count;
    }

    /// <summary>A booking addressed to one artisan: an exclusive offer with the longer direct window.
    /// The "New job request" notification is the caller's (it already exists).</summary>
    public async Task<JobOffer?> OfferDirectAsync(Booking booking, ArtisanProfile artisan, DateTimeOffset now, CancellationToken ct)
    {
        if (artisan.UserId is not { } userId) return null;
        var settings = await _settings.GetOrCreateAsync(ct);
        var offer = JobOffer.Create(booking.Id, artisan.Id, userId, booking.OfferRound + 1,
            JobOfferSource.Direct, now, now.AddMinutes(settings.DirectOfferWindowMinutes));
        _offers.Add(offer);
        booking.RecordOffer(now);
        return offer;
    }

    /// <summary>
    /// The chosen artisan declined, timed out, or the customer asked for someone else: close every
    /// live offer (the decliner's as Declined, the rest Withdrawn), reopen the request and put it out
    /// for bids.
    /// </summary>
    public async Task ReassignAsync(Booking booking, Guid? declinedByProfileId, string? reason, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var live in await _offers.ListLiveForBookingAsync(booking.Id, ct))
        {
            if (live.Status != JobOfferStatus.Offered) continue; // already expired/declined in this unit of work
            if (declinedByProfileId is { } p && live.ArtisanProfileId == p) live.Decline(now, reason);
            else live.Withdraw(now);
        }
        if (booking.Status != BookingStatus.Open) booking.Rebroadcast();
        await BroadcastAsync(booking, now, ct);
    }

    /// <summary>The artisan accepted their direct request through the plain accept action: close the offer as Accepted.</summary>
    public async Task AcceptDirectOfferAsync(Guid bookingId, Guid artisanProfileId, DateTimeOffset now, CancellationToken ct)
    {
        var offer = await _offers.FindLiveForArtisanAsync(bookingId, artisanProfileId, ct);
        if (offer is not null && offer.IsLive(now)) offer.Accept(now);
    }

    /// <summary>The customer cancelled or the job was taken: nothing stays offered.</summary>
    public async Task WithdrawAllAsync(Guid bookingId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var live in await _offers.ListLiveForBookingAsync(bookingId, ct)) live.Withdraw(now);
    }

    internal static string Area(Booking booking)
    {
        var first = (booking.AddressText ?? string.Empty).Split(',')[0].Trim();
        return string.IsNullOrWhiteSpace(first) ? "your area" : first;
    }
}
