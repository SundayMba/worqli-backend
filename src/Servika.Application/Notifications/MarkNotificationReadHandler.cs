using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;

namespace Servika.Application.Notifications;

/// <summary>
/// Marks one of the signed-in user's notifications read. Scoped to the owner — a
/// notification that isn't theirs is a 404, never mutable.
/// </summary>
public sealed class MarkNotificationReadHandler
{
    private readonly INotificationRepository _notifications;
    private readonly IClock _clock;

    public MarkNotificationReadHandler(INotificationRepository notifications, IClock clock)
    {
        _notifications = notifications;
        _clock = clock;
    }

    public async Task HandleAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var notification = await _notifications.FindForUserAsync(notificationId, userId, ct)
            ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");

        notification.MarkRead(_clock.UtcNow);
        await _notifications.SaveChangesAsync(ct);
    }
}
