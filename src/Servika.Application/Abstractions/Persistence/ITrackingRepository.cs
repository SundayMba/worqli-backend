using Servika.Domain.Tracking;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for live-tracking sessions, stated in Domain terms; Infrastructure
/// implements it with EF Core. One active session per booking at a time.
/// </summary>
public interface ITrackingRepository
{
    void Add(TrackingSession session);

    /// <summary>The active session for a booking, or null if none is running.</summary>
    Task<TrackingSession?> FindActiveByBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>Active sessions whose last update (or start, if never updated) is
    /// older than <paramref name="cutoffUtc"/> — the stale ones to end.</summary>
    Task<IReadOnlyList<TrackingSession>> ListStaleActiveAsync(
        DateTimeOffset cutoffUtc, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
