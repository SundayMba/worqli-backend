using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Servika.Api.Hubs;
using Servika.Application.Chat;
using Servika.Application.Common;
using Servika.Contracts.Chat;

namespace Servika.Api.Controllers;

/// <summary>
/// Conversations (PRD §Chat). A conversation is the two-party thread between a
/// customer and an artisan, keyed by that pair — so a customer can start one from an
/// artisan's profile before any booking exists, and every booking between them
/// resolves to the same thread. Used by <b>both</b> sides; every action is scoped by
/// <see cref="ChatService"/> to the two participants. Messages are sent + read over
/// REST (reliable, persisted); real-time delivery is a broadcast to the
/// conversation's <see cref="ChatHub"/> group right after a send.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[Tags("Chat")]
public sealed class ChatController : ControllerBase
{
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(IHubContext<ChatHub> hub)
    {
        _hub = hub;
    }

    /// <summary>Resolve-or-create the caller's conversation with an artisan (browse →
    /// "Chat"). Returns a pointer the app opens the thread with.</summary>
    /// <response code="200">The conversation.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such artisan.</response>
    [HttpPost("conversations/with-artisan/{artisanId:guid}")]
    [ProducesResponseType(typeof(ConversationRef), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationRef>> StartWithArtisan(
        Guid artisanId,
        [FromServices] ChatService chat,
        CancellationToken ct)
    {
        return Ok(await chat.StartWithArtisanAsync(CurrentUserId(), artisanId, ct));
    }

    /// <summary>Resolve-or-create the conversation attached to a booking's pair
    /// (either participant may call it).</summary>
    /// <response code="200">The conversation.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not a participant, or the booking has no artisan.</response>
    [HttpPost("conversations/for-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(ConversationRef), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationRef>> StartForBooking(
        Guid bookingId,
        [FromServices] ChatService chat,
        CancellationToken ct)
    {
        return Ok(await chat.StartForBookingAsync(CurrentUserId(), bookingId, ct));
    }

    /// <summary>The conversation thread (oldest first). Marks the other party's
    /// messages read.</summary>
    /// <response code="200">The messages.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not a participant in this conversation.</response>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(
        Guid conversationId,
        [FromServices] ChatService chat,
        CancellationToken ct)
    {
        return Ok(await chat.GetMessagesAsync(CurrentUserId(), conversationId, ct));
    }

    /// <summary>Send a message to the conversation.</summary>
    /// <response code="201">Message sent.</response>
    /// <response code="400">Empty or too-long message.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not a participant in this conversation.</response>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        [FromServices] ChatService chat,
        CancellationToken ct)
    {
        var message = await chat.SendMessageAsync(CurrentUserId(), conversationId, request.Body, ct);

        // Push to everyone watching this conversation's thread (incl. the sender, who
        // dedupes by id). Best-effort — the message is already persisted.
        await _hub.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("MessageReceived", message, ct);

        return CreatedAtAction(nameof(GetMessages), new { conversationId }, message);
    }

    /// <summary>The caller's conversations for the messages tab.</summary>
    /// <response code="200">Conversations, newest activity first.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> GetConversations(
        [FromServices] ChatService chat,
        CancellationToken ct)
    {
        return Ok(await chat.GetConversationsAsync(CurrentUserId(), ct));
    }

    /// <summary>The caller's total unread message count (messages-tab badge).</summary>
    /// <response code="200">The count.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("conversations/unread-count")]
    [ProducesResponseType(typeof(ChatUnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatUnreadCountDto>> GetUnreadCount(
        [FromServices] ChatService chat,
        CancellationToken ct)
    {
        var count = await chat.GetUnreadCountAsync(CurrentUserId(), ct);
        return Ok(new ChatUnreadCountDto(count));
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
            throw new InvalidCredentialsException();
        return userId;
    }
}
