namespace Servika.Application.Abstractions.Notifications;

/// <summary>
/// Port over a push-notification provider (Expo). Infrastructure decides how the
/// bytes actually go out; the Application layer only asks to notify some tokens.
/// </summary>
public interface IPushSender
{
    /// <summary>Best-effort delivery to the given device tokens. Never throws into
    /// the caller — push is a side effect, not part of any transaction.</summary>
    Task SendAsync(
        IReadOnlyCollection<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct);
}

/// <summary>
/// Fire-and-forget hand-off used by the <c>NotificationEmitter</c>: turns an in-app
/// notification into a device push without blocking (or being able to break) the
/// transaction that produced it. The implementation resolves the user's tokens in
/// its own scope and sends in the background.
/// </summary>
public interface INotificationPushDispatcher
{
    void Dispatch(Guid userId, string title, string body, Guid? bookingId);
}
