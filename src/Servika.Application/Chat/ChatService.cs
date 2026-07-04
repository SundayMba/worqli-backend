using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Chat;
using Servika.Domain.Bookings;
using Servika.Domain.Chat;

namespace Servika.Application.Chat;

/// <summary>
/// Orchestrates booking conversations — the use-case layer the chat controller and
/// hub call into. A conversation is strictly two-party: only the booking's
/// customer-owner and its assigned artisan may read or post. Participation is
/// decided by the booking's <b>own</b> customer/artisan ids — not the caller's role
/// claim — so it's correct even for an account that is the customer on one booking
/// and the artisan on another. A non-participant (or unknown booking) is a 404, so
/// the endpoint never reveals another user's conversation exists.
/// </summary>
public sealed class ChatService
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IChatRepository _chat;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public ChatService(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IChatRepository chat,
        IUserRepository users,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _chat = chat;
        _users = users;
        _clock = clock;
    }

    /// <summary>Resolves the caller's role in a booking's conversation, or throws
    /// <see cref="NotFoundException"/> if they aren't a participant.</summary>
    public async Task<ChatSenderRole> AuthorizeAccessAsync(
        Guid userId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindByIdAsync(bookingId, ct)
            ?? throw new NotFoundException("Conversation not found.");
        return await ResolveRoleAsync(userId, booking, ct)
            ?? throw new NotFoundException("Conversation not found.");
    }

    /// <summary>Posts a message to a booking's conversation (participants only).</summary>
    public async Task<ChatMessageDto> SendMessageAsync(
        Guid userId, Guid bookingId, string body, CancellationToken ct)
    {
        var role = await AuthorizeAccessAsync(userId, bookingId, ct);

        var message = ChatMessage.Create(bookingId, userId, role, body, _clock.UtcNow);
        _chat.Add(message);
        await _chat.SaveChangesAsync(ct);
        return message.ToDto();
    }

    /// <summary>The booking's thread (oldest first). Marks messages from the other
    /// party as read, since fetching the thread means the caller has seen them.</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid userId, Guid bookingId, CancellationToken ct)
    {
        await AuthorizeAccessAsync(userId, bookingId, ct);

        var changed = await _chat.MarkReadAsync(bookingId, userId, ct);
        if (changed > 0) await _chat.SaveChangesAsync(ct);

        var messages = await _chat.ListForBookingAsync(bookingId, ct);
        return messages.Select(m => m.ToDto()).ToList();
    }

    /// <summary>The caller's conversations for the messages tab — one per booking
    /// that has at least one message, newest activity first.</summary>
    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId, CancellationToken ct)
    {
        var bookings = await ParticipantBookingsAsync(userId, ct);
        if (bookings.Count == 0) return Array.Empty<ConversationDto>();

        var byId = bookings.ToDictionary(b => b.Id);
        var messages = await _chat.ListForBookingsAsync(byId.Keys.ToList(), ct);

        var conversations = new List<ConversationDto>();
        foreach (var group in messages.GroupBy(m => m.BookingId))
        {
            if (!byId.TryGetValue(group.Key, out var booking)) continue;

            var ordered = group.OrderBy(m => m.CreatedAt).ToList();
            var last = ordered[^1];
            var unread = ordered.Count(m => m.SenderUserId != userId && !m.IsRead);

            conversations.Add(new ConversationDto(
                booking.Id,
                booking.ArtisanId,
                await CounterpartyNameAsync(booking, userId, ct),
                booking.ServiceName,
                booking.Status.ToString(),
                last.Body,
                last.CreatedAt,
                unread));
        }

        return conversations.OrderByDescending(c => c.LastMessageAtUtc).ToList();
    }

    /// <summary>Total unread messages across the caller's conversations (tab badge).</summary>
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct)
    {
        var bookings = await ParticipantBookingsAsync(userId, ct);
        if (bookings.Count == 0) return 0;

        var messages = await _chat.ListForBookingsAsync(bookings.Select(b => b.Id).ToList(), ct);
        return messages.Count(m => m.SenderUserId != userId && !m.IsRead);
    }

    // Every booking the caller participates in — the ones they own as a customer
    // plus (if they have an artisan profile) the ones assigned to them. Unioned by
    // id so a hybrid account sees both sides without duplicates.
    private async Task<IReadOnlyList<Booking>> ParticipantBookingsAsync(Guid userId, CancellationToken ct)
    {
        var byId = new Dictionary<Guid, Booking>();

        foreach (var b in await _bookings.ListForCustomerAsync(userId, null, ct))
            byId[b.Id] = b;

        var profile = await _catalogue.GetArtisanByUserIdAsync(userId, ct);
        if (profile is not null)
            foreach (var b in await _bookings.ListForArtisanAsync(profile.Id, null, ct))
                byId[b.Id] = b;

        return byId.Values.ToList();
    }

    private async Task<ChatSenderRole?> ResolveRoleAsync(Guid userId, Booking booking, CancellationToken ct)
    {
        if (booking.CustomerId == userId)
            return ChatSenderRole.Customer;

        var profile = await _catalogue.GetArtisanByUserIdAsync(userId, ct);
        return profile is not null && booking.ArtisanId == profile.Id ? ChatSenderRole.Artisan : null;
    }

    // Who the caller is talking to: the customer sees the (denormalised) artisan
    // name on the booking; the artisan sees the customer's name.
    private async Task<string> CounterpartyNameAsync(Booking booking, Guid callerUserId, CancellationToken ct)
    {
        if (booking.CustomerId == callerUserId)
            return string.IsNullOrWhiteSpace(booking.ArtisanName) ? "Artisan" : booking.ArtisanName!;

        var customer = await _users.FindByIdAsync(booking.CustomerId, ct);
        return customer?.FullName ?? "Customer";
    }
}
