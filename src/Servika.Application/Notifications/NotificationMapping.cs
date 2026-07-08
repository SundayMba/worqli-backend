using Servika.Contracts.Notifications;
using Servika.Domain.Notifications;

namespace Servika.Application.Notifications;

/// <summary>Maps a notification Domain entity to its public DTO.</summary>
internal static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type.ToString(), n.Title, n.Body, n.BookingId, n.ConversationId, n.IsRead, n.CreatedAt);
}
