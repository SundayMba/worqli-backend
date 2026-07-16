using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Tracking;
using Servika.Domain.Bookings;
using Servika.Domain.Tracking;

namespace Servika.Application.Tracking;

/// <summary>
/// Orchestrates live tracking — the use-case layer the SignalR hub and the stale-
/// cleanup worker call into. It owns the rules: only the customer-owner or the
/// assigned artisan may watch a booking, only the assigned artisan may push
/// location, and only while the booking is <c>OnMyWay</c>. Ownership is resolved
/// the same way as the rest of the booking flow (artisan user → profile → the
/// booking's <c>ArtisanId</c>).
/// </summary>
public sealed class TrackingService
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly ITrackingRepository _tracking;
    private readonly IClock _clock;

    public TrackingService(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        ITrackingRepository tracking,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _tracking = tracking;
        _clock = clock;
    }

    /// <summary>
    /// Authorises a caller to join a booking's tracking group. The customer who
    /// owns the booking or the assigned artisan may join; anyone else is rejected.
    /// Deliberately role-claim-INDEPENDENT (like chat): after "Become a Pro" an
    /// account carries the Artisan role yet is still the CUSTOMER on its own
    /// bookings — the booking's own parties decide, never the token's role.
    /// Throws <see cref="TrackingNotAllowedException"/> when not permitted.
    /// </summary>
    public async Task AuthorizeJoinAsync(
        Guid userId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindByIdAsync(bookingId, ct)
            ?? throw new TrackingNotAllowedException("Booking not found.");

        var allowed = booking.CustomerId == userId
            || await IsAssignedArtisanAsync(userId, booking, ct);

        if (!allowed)
            throw new TrackingNotAllowedException("You cannot track this booking.");
    }

    /// <summary>
    /// Records the assigned artisan's latest position, opening a session on the
    /// first ping. Validates ownership, the booking state (<c>OnMyWay</c>) and the
    /// coordinates. Throws <see cref="TrackingNotAllowedException"/> if rejected.
    /// </summary>
    public async Task<LocationRecorded> RecordLocationAsync(
        Guid artisanUserId, Guid bookingId,
        double latitude, double longitude,
        double? accuracy, double? heading, double? speed,
        CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new TrackingNotAllowedException("No artisan profile is linked to this account.");

        var booking = await _bookings.FindByIdAsync(bookingId, ct)
            ?? throw new TrackingNotAllowedException("Booking not found.");

        if (booking.ArtisanId != profile.Id)
            throw new TrackingNotAllowedException("This booking isn't assigned to you.");

        if (booking.Status != BookingStatus.OnMyWay)
            throw new TrackingNotAllowedException("Tracking is only available while you are on the way.");

        var now = _clock.UtcNow;
        var session = await _tracking.FindActiveByBookingAsync(bookingId, ct);
        var started = false;
        if (session is null)
        {
            session = TrackingSession.Start(bookingId, profile.Id, booking.CustomerId, now);
            _tracking.Add(session);
            started = true;
        }

        // Throws ArgumentOutOfRange on a bad fix — the hub maps that to TrackingError.
        session.UpdateLocation(latitude, longitude, accuracy, heading, speed, now);
        await _tracking.SaveChangesAsync(ct);

        var update = new LocationUpdate(bookingId, latitude, longitude, accuracy, heading, speed, now);
        return new LocationRecorded(started, update);
    }

    /// <summary>Ends the active session for a booking, if any. Returns true if one
    /// was ended (e.g. on the artisan's arrival).</summary>
    public async Task<bool> EndAsync(Guid bookingId, CancellationToken ct)
    {
        var session = await _tracking.FindActiveByBookingAsync(bookingId, ct);
        if (session is null) return false;

        session.End(_clock.UtcNow);
        await _tracking.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Ends sessions that haven't had an update within
    /// <paramref name="olderThan"/>. Returns the booking ids that were ended, so
    /// the caller can notify their tracking groups.</summary>
    public async Task<IReadOnlyList<Guid>> EndStaleAsync(TimeSpan olderThan, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var stale = await _tracking.ListStaleActiveAsync(now - olderThan, ct);
        if (stale.Count == 0) return Array.Empty<Guid>();

        foreach (var session in stale)
            session.End(now);

        await _tracking.SaveChangesAsync(ct);
        return stale.Select(s => s.BookingId).ToList();
    }

    private async Task<bool> IsAssignedArtisanAsync(Guid userId, Booking booking, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(userId, ct);
        return profile is not null && booking.ArtisanId == profile.Id;
    }
}
