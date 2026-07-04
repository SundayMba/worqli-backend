using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Notifications;

namespace Servika.Api.Controllers;

/// <summary>
/// In-app notification feed for the signed-in user — the bell on Home. Every
/// action is scoped to the caller; a notification that isn't theirs is a 404.
/// Notifications are produced as a side effect of booking transitions and settled
/// payments (see <c>NotificationEmitter</c>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
[Tags("Notifications")]
public sealed class NotificationsController : ControllerBase
{
    /// <summary>The current user's notifications, newest first.</summary>
    /// <response code="200">The notification feed (empty if none).</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(
        [FromServices] GetNotificationsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>The current user's unread count (for the bell badge).</summary>
    /// <response code="200">The unread count.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(
        [FromServices] GetUnreadCountHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Mark one notification read.</summary>
    /// <response code="204">Marked read (idempotent).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such notification owned by this user.</response>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(
        Guid id,
        [FromServices] MarkNotificationReadHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Mark all the current user's notifications read.</summary>
    /// <response code="204">All marked read.</response>
    /// <response code="401">Not signed in.</response>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead(
        [FromServices] MarkAllNotificationsReadHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), ct);
        return NoContent();
    }

    /// <summary>Register this device's Expo push token so the user gets push notifications.</summary>
    /// <response code="204">Token registered.</response>
    /// <response code="400">Missing token.</response>
    /// <response code="401">Not signed in.</response>
    [HttpPost("push-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterPushToken(
        [FromBody] RegisterPushTokenRequest request,
        [FromServices] RegisterPushTokenHandler handler,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("A push token is required.");
        await handler.HandleAsync(CurrentUserId(), request.Token, request.Platform, ct);
        return NoContent();
    }

    /// <summary>Remove this device's push token (e.g. on logout).</summary>
    /// <response code="204">Token removed (idempotent).</response>
    /// <response code="401">Not signed in.</response>
    [HttpDelete("push-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemovePushToken(
        [FromBody] RemovePushTokenRequest request,
        [FromServices] RemovePushTokenHandler handler,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Token))
            await handler.HandleAsync(request.Token, ct);
        return NoContent();
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
