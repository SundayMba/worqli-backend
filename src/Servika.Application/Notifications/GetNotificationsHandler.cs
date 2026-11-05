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

    /// <param name="audience">"customer" / "artisan" to list only that app's notices (plus shared ones);
    /// null lists everything.</param>
    public async Task<IReadOnlyList<NotificationDto>> HandleAsync(Guid userId, string? audience, CancellationToken ct)
    {
        var items = await _notifications.ListForUserAsync(userId, NotificationAudienceFilter.ParseQuery(audience), ct);
        return items.Select(n => n.ToDto()).ToList();
    }
}
