namespace Servika.Application.Abstractions.Notifications;

/// <summary>
/// Port over real-time (SignalR) delivery of in-app notifications, so a signed-in
/// user's app updates the instant something happens — no waiting on the next poll.
/// Best-effort like push: implemented in the Api host (where the hub lives) and
/// invoked from the background dispatcher, never from inside a transaction.
/// </summary>
public interface INotificationRealtimePublisher
{
    /// <summary>Broadcasts a "NotificationReceived" event to the user's devices.</summary>
    Task PublishAsync(
        Guid userId,
        string title,
        string body,
        Guid? bookingId,
        Guid? conversationId,
        CancellationToken ct);
}
