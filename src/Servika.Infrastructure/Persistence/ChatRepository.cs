using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Chat;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IChatRepository"/>. Shares the scoped
/// <see cref="ServikaDbContext"/> so a sent message flushes in one transaction.
/// </summary>
public sealed class ChatRepository : IChatRepository
{
    private readonly ServikaDbContext _db;

    public ChatRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(ChatMessage message) => _db.ChatMessages.Add(message);

    public async Task<IReadOnlyList<ChatMessage>> ListForBookingAsync(Guid bookingId, CancellationToken ct) =>
        await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.BookingId == bookingId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChatMessage>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds, CancellationToken ct)
    {
        if (bookingIds.Count == 0) return Array.Empty<ChatMessage>();

        return await _db.ChatMessages
            .AsNoTracking()
            .Where(m => bookingIds.Contains(m.BookingId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<int> MarkReadAsync(Guid bookingId, Guid readerUserId, CancellationToken ct) =>
        _db.ChatMessages
            .Where(m => m.BookingId == bookingId && m.SenderUserId != readerUserId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
