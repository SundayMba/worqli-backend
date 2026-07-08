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

    public void AddConversation(Conversation conversation) => _db.Conversations.Add(conversation);

    public Task<Conversation?> FindConversationAsync(Guid customerUserId, Guid artisanId, CancellationToken ct) =>
        _db.Conversations
            .FirstOrDefaultAsync(c => c.CustomerUserId == customerUserId && c.ArtisanId == artisanId, ct);

    public Task<Conversation?> FindConversationByIdAsync(Guid conversationId, CancellationToken ct) =>
        _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);

    public async Task<IReadOnlyList<Conversation>> ListConversationsForUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Conversations
            .AsNoTracking()
            .Where(c => c.CustomerUserId == userId || c.ArtisanUserId == userId)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .ToListAsync(ct);

    public void AddMessage(ChatMessage message) => _db.ChatMessages.Add(message);

    public async Task<IReadOnlyList<ChatMessage>> ListForConversationAsync(Guid conversationId, CancellationToken ct) =>
        await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChatMessage>> ListForConversationsAsync(
        IReadOnlyCollection<Guid> conversationIds, CancellationToken ct)
    {
        if (conversationIds.Count == 0) return Array.Empty<ChatMessage>();

        return await _db.ChatMessages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<int> MarkReadAsync(Guid conversationId, Guid readerUserId, CancellationToken ct) =>
        _db.ChatMessages
            .Where(m => m.ConversationId == conversationId && m.SenderUserId != readerUserId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
