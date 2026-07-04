using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Notifications;

namespace Servika.Application.Notifications;

/// <summary>Lists the signed-in user's notifications, newest first.</summary>
public sealed class GetNotificationsHandler
{
    private readonly INotificationRepository _notifications;

    public GetNotificationsHandler(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<NotificationDto>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var items = await _notifications.ListForUserAsync(userId, ct);
        return items.Select(n => n.ToDto()).ToList();
    }
}
