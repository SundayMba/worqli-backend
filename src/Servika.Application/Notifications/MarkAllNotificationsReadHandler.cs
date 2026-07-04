using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;

namespace Servika.Application.Notifications;

/// <summary>Marks all of the signed-in user's unread notifications read.</summary>
public sealed class MarkAllNotificationsReadHandler
{
    private readonly INotificationRepository _notifications;
    private readonly IClock _clock;

    public MarkAllNotificationsReadHandler(INotificationRepository notifications, IClock clock)
    {
        _notifications = notifications;
        _clock = clock;
    }

    public Task HandleAsync(Guid userId, CancellationToken ct) =>
        _notifications.MarkAllReadAsync(userId, _clock.UtcNow, ct);
}
