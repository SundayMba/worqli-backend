using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Notifications;

namespace Servika.Application.Notifications;

/// <summary>Returns the signed-in user's unread notification count (badge).</summary>
public sealed class GetUnreadCountHandler
{
    private readonly INotificationRepository _notifications;

    public GetUnreadCountHandler(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task<UnreadCountDto> HandleAsync(Guid userId, CancellationToken ct) =>
        new(await _notifications.CountUnreadAsync(userId, ct));
}
