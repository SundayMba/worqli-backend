using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Servika.Api.Hubs;

/// <summary>
/// Real-time in-app notification delivery. A signed-in client connects (JWT via
/// the <c>access_token</c> query param, like the other hubs) and is auto-joined
/// to its per-user group; the <c>SignalRNotificationPublisher</c> broadcasts
/// <c>NotificationReceived</c> there whenever the emitter produces a notification
/// — so the bell badge / job lists refresh the instant something happens instead
/// of on the next poll. No client → server methods; it's receive-only.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    /// <summary>The SignalR group carrying one user's notifications.</summary>
    public static string GroupName(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));

        await base.OnConnectedAsync();
    }
}
