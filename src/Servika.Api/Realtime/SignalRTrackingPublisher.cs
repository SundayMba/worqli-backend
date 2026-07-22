using Microsoft.AspNetCore.SignalR;
using Servika.Api.Hubs;
using Servika.Application.Abstractions.Tracking;

namespace Servika.Api.Realtime;

/// <summary>
/// Api-side implementation of <see cref="ITrackingRealtimePublisher"/> — broadcasts
/// to a booking's <see cref="TrackingHub"/> group. Lives here (not Infrastructure)
/// because the hub type does; registered only in the Api, so the stale-session sweep
/// broadcasts when hosted in the Api and no-ops when hosted in the worker.
/// </summary>
public sealed class SignalRTrackingPublisher : ITrackingRealtimePublisher
{
    private readonly IHubContext<TrackingHub> _hub;

    public SignalRTrackingPublisher(IHubContext<TrackingHub> hub)
    {
        _hub = hub;
    }

    public Task TrackingEndedAsync(Guid bookingId, string reason, CancellationToken ct) =>
        _hub.Clients
            .Group($"booking:{bookingId}")
            .SendAsync("TrackingEnded", new { bookingId, reason }, ct);
}
