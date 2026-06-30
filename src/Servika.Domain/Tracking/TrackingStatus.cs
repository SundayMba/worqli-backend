namespace Servika.Domain.Tracking;

/// <summary>
/// Lifecycle of a live-tracking session. A session is <see cref="Active"/> while
/// the artisan is en route and pushing location updates, then <see cref="Ended"/>
/// once they arrive, the trip is cancelled, or it goes stale (no updates).
/// Stored as a readable string.
/// </summary>
public enum TrackingStatus
{
    Active = 0,
    Ended = 1,
}
