using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Chat;
using Servika.Domain.Chat;

namespace Servika.Application.Chat;

/// <summary>
/// Orchestrates conversations — the use-case layer the chat controller and hub call
/// into. A conversation is the two-party thread between a <b>customer</b> and an
/// <b>artisan</b>, identified by that pair (not by a booking), so it can start before
/// any booking exists and spans all their bookings afterwards. Participation is
/// decided by the conversation's own <c>CustomerUserId</c>/<c>ArtisanUserId</c> — not
/// the caller's role claim — so it's correct even for an account that is the customer
/// in one conversation and the artisan in another. A non-participant (or unknown
/// conversation) is a 404, so an endpoint never reveals another user's thread exists.
/// </summary>
public sealed class ChatService
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IChatRepository _chat;
    private readonly IUserRepository _users;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    private readonly Common.AuthPolicyOptions _authPolicy;

    public ChatService(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IChatRepository chat,
        IUserRepository users,
        Common.AuthPolicyOptions authPolicy,
        Notifications.NotificationEmitter notifications,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _chat = chat;
        _users = users;
        _authPolicy = authPolicy;
        _notifications = notifications;
        _clock = clock;
    }

    /// <summary>Resolve-or-create the caller's conversation with an artisan. The
    /// caller is always the customer side here (browse → "Chat"). Unknown artisan → 404.</summary>
    public async Task<ConversationRef> StartWithArtisanAsync(
        Guid customerUserId, Guid artisanId, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException("Artisan not found.");

        // Phone-verification gate (off by default) — opening a chat is a "first
        // contact" moment where a reachable number matters.
        if (_authPolicy.RequirePhoneForBooking)
        {
            var customer = await _users.FindByIdAsync(customerUserId, ct);
            if (customer is { IsPhoneVerified: false })
                throw new Common.PhoneVerificationRequiredException();
        }

        var conversation = await _chat.FindConversationAsync(customerUserId, artisanId, ct);
        if (conversation is null)
        {
            conversation = Conversation.Start(customerUserId, artisanId, artisan.UserId, _clock.UtcNow);
            _chat.AddConversation(conversation);
            await _chat.SaveChangesAsync(ct);
        }

        return new ConversationRef(conversation.Id, artisanId, artisan.FullName);
    }

    /// <summary>Resolve-or-create the conversation attached to a booking's pair. Either
    /// participant (the booking's customer or its assigned artisan) may call this;
    /// anyone else — or a booking with no artisan — is a 404.</summary>
    public async Task<ConversationRef> StartForBookingAsync(
        Guid userId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindByIdAsync(bookingId, ct)
            ?? throw new NotFoundException("Booking not found.");
        if (booking.ArtisanId is null)
            throw new NotFoundException("Booking not found.");

        var artisan = await _catalogue.GetArtisanByIdAsync(booking.ArtisanId.Value, ct);
        var isCustomer = booking.CustomerId == userId;
        var isArtisan = artisan?.UserId is not null && artisan.UserId == userId;
        if (!isCustomer && !isArtisan)
            throw new NotFoundException("Booking not found.");

        var conversation = await _chat.FindConversationAsync(booking.CustomerId, booking.ArtisanId.Value, ct);
        if (conversation is null)
        {
            conversation = Conversation.Start(
                booking.CustomerId, booking.ArtisanId.Value, artisan?.UserId, _clock.UtcNow);
            _chat.AddConversation(conversation);
            await _chat.SaveChangesAsync(ct);
        }

        var counterpartyName = isCustomer
            ? (artisan?.FullName ?? booking.ArtisanName ?? "Artisan")
            : (await _users.FindByIdAsync(booking.CustomerId, ct))?.FullName ?? "Customer";

        return new ConversationRef(conversation.Id, booking.ArtisanId.Value, counterpartyName);
    }

    /// <summary>Resolve the caller's role in a conversation, or throw
    /// <see cref="NotFoundException"/> if they aren't a participant.</summary>
    public async Task<ChatSenderRole> AuthorizeAccessAsync(
        Guid userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await _chat.FindConversationByIdAsync(conversationId, ct)
            ?? throw new NotFoundException("Conversation not found.");
        return RoleFor(conversation, userId)
            ?? throw new NotFoundException("Conversation not found.");
    }

    /// <summary>Posts a message to a conversation (participants only).</summary>
    public async Task<ChatMessageDto> SendMessageAsync(
        Guid userId, Guid conversationId, string body, CancellationToken ct)
    {
        var conversation = await _chat.FindConversationByIdAsync(conversationId, ct)
            ?? throw new NotFoundException("Conversation not found.");
        var role = RoleFor(conversation, userId)
            ?? throw new NotFoundException("Conversation not found.");

        var now = _clock.UtcNow;
        // Redact contact details before persisting — keeps the job (and its escrow /
        // dispute cover) on Servika. Applied here so a modified client can't skip it.
        var safeBody = ChatContentPolicy.Redact(body);
        var message = ChatMessage.Create(conversationId, userId, role, safeBody, now);
        _chat.AddMessage(message);
        conversation.Touch(now);

        // Notify the other party (in-app + push). Staged onto the shared DbContext, so
        // it commits in the same SaveChanges as the message.
        await NotifyRecipientAsync(conversation, role, safeBody, ct);

        await _chat.SaveChangesAsync(ct);
        return message.ToDto();
    }

    /// <summary>The conversation's thread (oldest first). Marks messages from the
    /// other party as read, since fetching the thread means the caller has seen them.</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid userId, Guid conversationId, CancellationToken ct)
    {
        await AuthorizeAccessAsync(userId, conversationId, ct);

        var changed = await _chat.MarkReadAsync(conversationId, userId, ct);
        if (changed > 0) await _chat.SaveChangesAsync(ct);

        var messages = await _chat.ListForConversationAsync(conversationId, ct);
        return messages.Select(m => m.ToDto()).ToList();
    }

    /// <summary>The caller's conversations for the messages tab — one per thread that
    /// has at least one message, newest activity first.</summary>
    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId, CancellationToken ct)
    {
        var conversations = await _chat.ListConversationsForUserAsync(userId, ct);
        if (conversations.Count == 0) return Array.Empty<ConversationDto>();

        var byId = conversations.ToDictionary(c => c.Id);
        var messages = await _chat.ListForConversationsAsync(byId.Keys.ToList(), ct);

        var rows = new List<ConversationDto>();
        foreach (var group in messages.GroupBy(m => m.ConversationId))
        {
            if (!byId.TryGetValue(group.Key, out var conversation)) continue;

            var ordered = group.OrderBy(m => m.CreatedAt).ToList();
            var last = ordered[^1];
            var unread = ordered.Count(m => m.SenderUserId != userId && !m.IsRead);

            rows.Add(new ConversationDto(
                conversation.Id,
                conversation.ArtisanId,
                await CounterpartyNameAsync(conversation, userId, ct),
                last.Body,
                last.CreatedAt,
                unread));
        }

        return rows.OrderByDescending(c => c.LastMessageAtUtc).ToList();
    }

    /// <summary>Total unread messages across the caller's conversations (tab badge).</summary>
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct)
    {
        var conversations = await _chat.ListConversationsForUserAsync(userId, ct);
        if (conversations.Count == 0) return 0;

        var messages = await _chat.ListForConversationsAsync(conversations.Select(c => c.Id).ToList(), ct);
        return messages.Count(m => m.SenderUserId != userId && !m.IsRead);
    }

    // Notify the party who DIDN'T send: if the customer sent, notify the artisan's
    // login (skipped when the profile has no linked user); if the artisan sent, notify
    // the customer. The sender's display name becomes the notification title.
    private async Task NotifyRecipientAsync(
        Conversation conversation, ChatSenderRole senderRole, string preview, CancellationToken ct)
    {
        Guid? recipientUserId;
        string senderName;

        if (senderRole == ChatSenderRole.Customer)
        {
            recipientUserId = conversation.ArtisanUserId;
            senderName = (await _users.FindByIdAsync(conversation.CustomerUserId, ct))?.FullName ?? "Customer";
        }
        else
        {
            recipientUserId = conversation.CustomerUserId;
            var artisan = await _catalogue.GetArtisanByIdAsync(conversation.ArtisanId, ct);
            senderName = string.IsNullOrWhiteSpace(artisan?.FullName) ? "Your artisan" : artisan!.FullName;
        }

        if (recipientUserId is not { } recipient) return; // unlinked artisan profile
        await _notifications.ChatMessageReceivedAsync(recipient, senderName, preview, conversation.Id, ct);
    }

    private static ChatSenderRole? RoleFor(Conversation conversation, Guid userId)
    {
        if (conversation.CustomerUserId == userId) return ChatSenderRole.Customer;
        if (conversation.ArtisanUserId is not null && conversation.ArtisanUserId == userId)
            return ChatSenderRole.Artisan;
        return null;
    }

    // Who the caller is talking to: the customer sees the artisan's name; the artisan
    // sees the customer's name.
    private async Task<string> CounterpartyNameAsync(Conversation conversation, Guid callerUserId, CancellationToken ct)
    {
        if (conversation.CustomerUserId == callerUserId)
        {
            var artisan = await _catalogue.GetArtisanByIdAsync(conversation.ArtisanId, ct);
            return string.IsNullOrWhiteSpace(artisan?.FullName) ? "Artisan" : artisan!.FullName;
        }

        var customer = await _users.FindByIdAsync(conversation.CustomerUserId, ct);
        return customer?.FullName ?? "Customer";
    }
}
