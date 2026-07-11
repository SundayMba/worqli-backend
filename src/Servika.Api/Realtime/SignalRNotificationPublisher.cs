using Microsoft.AspNetCore.SignalR;
using Servika.Api.Hubs;
using Servika.Application.Abstractions.Notifications;

namespace Servika.Api.Realtime;

/// <summary>
/// Api-side implementation of <see cref="INotificationRealtimePublisher"/> —
/// broadcasts to the user's <see cref="NotificationsHub"/> group. Lives here
/// (not Infrastructure) because the hub type does.
/// </summary>
public sealed class SignalRNotificationPublisher : INotificationRealtimePublisher
{
    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationsHub> hub)
    {
        _hub = hub;
    }

    public Task PublishAsync(
        Guid userId, string title, string body,
        Guid? bookingId, Guid? conversationId, CancellationToken ct) =>
        _hub.Clients
            .Group(NotificationsHub.GroupName(userId))
            .SendAsync(
                "NotificationReceived",
                new { title, body, bookingId, conversationId },
                ct);
}
