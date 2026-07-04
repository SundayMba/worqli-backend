namespace Servika.Domain.Notifications;

/// <summary>
/// An in-app notification delivered to one recipient (a <c>User</c>) — e.g. "your
/// artisan is on the way" or "payment received". Like every Domain entity it knows
/// nothing about the database or the web; it only holds its data and the one rule
/// it owns (read is one-way and stamps a time).
///
/// <para>An optional <see cref="BookingId"/> lets the client deep-link the tap to
/// the relevant booking. Title/body are pre-rendered by the emitter so the feed
/// renders without lookups.</para>
/// </summary>
public sealed class Notification
{
    public Guid Id { get; private set; }

    /// <summary>The recipient (a <c>User</c>).</summary>
    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>Booking to open when the notification is tapped, if any.</summary>
    public Guid? BookingId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    // EF Core rebuilds rows through this; private so app code can't skip the rules.
    private Notification() { }

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        Guid? bookingId,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Recipient is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Body = body?.Trim() ?? string.Empty,
            BookingId = bookingId,
            IsRead = false,
            CreatedAt = now,
        };
    }

    /// <summary>Marks the notification read (idempotent, stamps the time once).</summary>
    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead) return;
        IsRead = true;
        ReadAtUtc = now;
    }
}
