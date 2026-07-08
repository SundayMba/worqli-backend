using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Notifications;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="INotificationRepository"/>. Shares the
/// scoped <see cref="ServikaDbContext"/> with the other repositories, so a
/// notification queued by <see cref="NotificationRepository.Add"/> from an event
/// emitter is flushed in the same transaction as the change that triggered it.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly ServikaDbContext _db;

    public NotificationRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Notification notification) => _db.Notifications.Add(notification);

    public async Task<IReadOnlyList<Notification>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public Task<bool> HasUnreadChatAsync(Guid userId, Guid conversationId, CancellationToken ct) =>
        _db.Notifications.AnyAsync(
            n => n.UserId == userId
                 && n.Type == NotificationType.Chat
                 && n.ConversationId == conversationId
                 && !n.IsRead,
            ct);

    // Tracked (no AsNoTracking) so MarkRead persists on SaveChanges.
    public Task<Notification?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct) =>
        _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAtUtc, now),
                ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
