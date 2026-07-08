namespace Servika.Contracts.Chat;

/// <summary>
/// One message in a conversation. <see cref="SenderRole"/> is "Customer" or
/// "Artisan"; the app decides "me vs them" by comparing <see cref="SenderUserId"/>
/// to the signed-in user.
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderRole,
    string Body,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Send a message to a conversation (POST /conversations/{id}/messages).</summary>
public sealed record SendMessageRequest(string Body);

/// <summary>
/// A conversation row for the messages tab. <see cref="CounterpartyName"/> is the
/// other party from the caller's perspective.
/// </summary>
public sealed record ConversationDto(
    Guid Id,
    Guid ArtisanId,
    string CounterpartyName,
    string LastMessage,
    DateTimeOffset LastMessageAtUtc,
    int UnreadCount);

/// <summary>
/// A lightweight pointer to a conversation, returned by the resolve endpoints so the
/// app can open the thread even when it has no messages yet.
/// </summary>
public sealed record ConversationRef(Guid Id, Guid ArtisanId, string CounterpartyName);

/// <summary>The signed-in user's total unread message count (messages-tab badge).</summary>
public sealed record ChatUnreadCountDto(int Count);
