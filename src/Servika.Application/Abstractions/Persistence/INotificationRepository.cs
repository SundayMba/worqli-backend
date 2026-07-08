using Servika.Domain.Notifications;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for in-app notifications, stated in Domain terms; Infrastructure
/// implements it with EF Core. Reads/writes are scoped to the recipient user so
/// one user can never see or mutate another's notifications. Shares the scoped
/// <c>ServikaDbContext</c>, so an <see cref="Add"/> from an event emitter flushes
/// in the same transaction as the handler that triggered it.
/// </summary>
public interface INotificationRepository
{
    void Add(Notification notification);

    /// <summary>The user's notifications, newest first.</summary>
    Task<IReadOnlyList<Notification>> ListForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>How many of the user's notifications are unread (badge count).</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct);

    /// <summary>Whether the user already has an unread chat notification for this
    /// conversation — used to coalesce a burst of messages into one feed entry.</summary>
    Task<bool> HasUnreadChatAsync(Guid userId, Guid conversationId, CancellationToken ct);

    /// <summary>A single notification owned by this user, tracked so it can be
    /// marked read, or null if not found.</summary>
    Task<Notification?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct);

    /// <summary>Marks all of the user's unread notifications read; returns the count
    /// affected.</summary>
    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
