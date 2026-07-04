using Servika.Contracts.Chat;
using Servika.Domain.Chat;

namespace Servika.Application.Chat;

/// <summary>Maps chat Domain entities to their public DTOs.</summary>
internal static class ChatMapping
{
    public static ChatMessageDto ToDto(this ChatMessage m) =>
        new(m.Id, m.BookingId, m.SenderUserId, m.SenderRole.ToString(), m.Body, m.IsRead, m.CreatedAt);
}
