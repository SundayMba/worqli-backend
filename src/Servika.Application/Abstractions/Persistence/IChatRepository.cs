using Servika.Domain.Chat;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for booking conversations. Shares the scoped <c>ServikaDbContext</c>.
/// </summary>
public interface IChatRepository
{
    void Add(ChatMessage message);

    /// <summary>A booking's messages, oldest first (the thread order).</summary>
    Task<IReadOnlyList<ChatMessage>> ListForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>All messages across several bookings (for the conversations list),
    /// oldest first. Empty collection → no query.</summary>
    Task<IReadOnlyList<ChatMessage>> ListForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds, CancellationToken ct);

    /// <summary>Marks messages in a booking sent by the <b>other</b> party as read.
    /// Returns how many rows changed.</summary>
    Task<int> MarkReadAsync(Guid bookingId, Guid readerUserId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
