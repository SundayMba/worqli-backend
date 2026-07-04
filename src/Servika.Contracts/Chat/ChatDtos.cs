namespace Servika.Contracts.Chat;

/// <summary>
/// One message in a booking's conversation. <see cref="SenderRole"/> is "Customer"
/// or "Artisan"; the app decides "me vs them" by comparing <see cref="SenderUserId"/>
/// to the signed-in user.
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid BookingId,
    Guid SenderUserId,
    string SenderRole,
    string Body,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Send a message to a booking's conversation (POST /bookings/{id}/messages).</summary>
public sealed record SendMessageRequest(string Body);

/// <summary>
/// A conversation row for the messages tab — one per booking that has a thread.
/// <see cref="CounterpartyName"/> is the other party from the caller's perspective.
/// </summary>
public sealed record ConversationDto(
    Guid BookingId,
    Guid? ArtisanId,
    string CounterpartyName,
    string ServiceName,
    string BookingStatus,
    string LastMessage,
    DateTimeOffset LastMessageAtUtc,
    int UnreadCount);

/// <summary>The signed-in user's total unread message count (messages-tab badge).</summary>
public sealed record ChatUnreadCountDto(int Count);
