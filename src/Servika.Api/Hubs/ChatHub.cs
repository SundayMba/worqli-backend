using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Servika.Application.Chat;
using Servika.Application.Common;

namespace Servika.Api.Hubs;

/// <summary>
/// Real-time delivery for a booking's conversation. Clients connect with their JWT
/// (the <c>access_token</c> query param — WebSockets can't send the Authorization
/// header) and join a per-booking group; messages themselves are sent over REST
/// (reliable + persisted) and the controller broadcasts <c>MessageReceived</c> to
/// this group. The hub only guards group membership (participants only).
///
/// Client → server: <c>JoinConversation</c>, <c>LeaveConversation</c>.
/// Server → client: <c>MessageReceived</c> (broadcast by the chat controller),
/// <c>ChatError</c>.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ChatService _chat;

    public ChatHub(ChatService chat)
    {
        _chat = chat;
    }

    /// <summary>The SignalR group carrying a booking's conversation.</summary>
    public static string GroupName(Guid bookingId) => $"chat:{bookingId}";

    /// <summary>Join a booking's conversation group (participants only).</summary>
    public async Task JoinConversation(Guid bookingId)
    {
        try
        {
            await _chat.AuthorizeAccessAsync(CurrentUserId(), bookingId, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(bookingId));
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ChatError", new { message = SafeMessage(ex) });
        }
    }

    /// <summary>Leave a booking's conversation group.</summary>
    public Task LeaveConversation(Guid bookingId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(bookingId));

    private static string SafeMessage(Exception ex) =>
        ex is NotFoundException or ArgumentException ? ex.Message : "Chat error.";

    private Guid CurrentUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
            throw new NotFoundException("Not signed in.");

        return userId;
    }
}
