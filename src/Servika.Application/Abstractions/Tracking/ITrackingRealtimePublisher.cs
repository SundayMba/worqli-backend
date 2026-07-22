namespace Servika.Application.Abstractions.Tracking;

/// <summary>
/// Port over real-time (SignalR) delivery of tracking-lifecycle events. Implemented
/// in the host that owns the tracking hub (the Api); the stale-session sweep resolves
/// it <b>optionally</b> — when the sweep runs in the <c>Servika.Worker</c> process
/// (no hub there), it's absent and the broadcast is skipped. Cross-process delivery
/// would need a SignalR Redis backplane (not wired yet); the sweep still ends the
/// stale sessions in the database regardless.
/// </summary>
public interface ITrackingRealtimePublisher
{
    /// <summary>Tells a booking's tracking group the session ended (e.g. "stale").</summary>
    Task TrackingEndedAsync(Guid bookingId, string reason, CancellationToken ct);
}
