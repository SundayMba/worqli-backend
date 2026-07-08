using Servika.Domain.Chat;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for conversations + their messages. Shares the scoped
/// <c>ServikaDbContext</c>.
/// </summary>
public interface IChatRepository
{
    void AddConversation(Conversation conversation);

    /// <summary>The conversation for a (customer, artisan) pair, or null.</summary>
    Task<Conversation?> FindConversationAsync(Guid customerUserId, Guid artisanId, CancellationToken ct);

    Task<Conversation?> FindConversationByIdAsync(Guid conversationId, CancellationToken ct);

    /// <summary>Every conversation the user is part of — as the customer
    /// (<c>CustomerUserId</c>) or as the artisan (<c>ArtisanUserId</c>).</summary>
    Task<IReadOnlyList<Conversation>> ListConversationsForUserAsync(Guid userId, CancellationToken ct);

    void AddMessage(ChatMessage message);

    /// <summary>A conversation's messages, oldest first (the thread order).</summary>
    Task<IReadOnlyList<ChatMessage>> ListForConversationAsync(Guid conversationId, CancellationToken ct);

    /// <summary>All messages across several conversations (for the list), oldest
    /// first. Empty collection → no query.</summary>
    Task<IReadOnlyList<ChatMessage>> ListForConversationsAsync(
        IReadOnlyCollection<Guid> conversationIds, CancellationToken ct);

    /// <summary>Marks messages in a conversation sent by the <b>other</b> party as
    /// read. Returns how many rows changed.</summary>
    Task<int> MarkReadAsync(Guid conversationId, Guid readerUserId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
