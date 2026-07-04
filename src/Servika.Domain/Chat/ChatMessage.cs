namespace Servika.Domain.Chat;

/// <summary>
/// One message in a booking's conversation. A booking has a single two-party thread
/// between its customer-owner and its assigned artisan; every message is scoped to
/// the booking. Messages are persisted (unlike ephemeral tracking pings) so history
/// survives and both parties can catch up.
///
/// <para><see cref="IsRead"/> means "seen by the recipient" — set true when the
/// other party opens the thread, which drives the unread badge on the messages tab.</para>
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; private set; }

    /// <summary>The booking whose conversation this message belongs to.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>The <c>User</c> who sent it (customer or artisan account).</summary>
    public Guid SenderUserId { get; private set; }

    /// <summary>Which side sent it — lets a client render "me" vs "them".</summary>
    public ChatSenderRole SenderRole { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>Seen by the recipient (the other party opened the thread).</summary>
    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private ChatMessage() { }

    /// <summary>Maximum message length accepted.</summary>
    public const int MaxLength = 2000;

    public static ChatMessage Create(
        Guid bookingId, Guid senderUserId, ChatSenderRole senderRole, string body, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (senderUserId == Guid.Empty)
            throw new ArgumentException("Sender is required.", nameof(senderUserId));

        var trimmed = (body ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("A message can't be empty.", nameof(body));
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"A message can be at most {MaxLength} characters.", nameof(body));

        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            SenderUserId = senderUserId,
            SenderRole = senderRole,
            Body = trimmed,
            IsRead = false,
            CreatedAt = now,
        };
    }
}
