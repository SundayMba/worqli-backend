using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>Phone notifications for the installed admin console (Web Push).</summary>
[ApiController]
[Route("api/v1/admin/push")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Produces("application/json")]
public sealed class AdminPushController : ControllerBase
{
    /// <summary>The VAPID public key the browser subscribes with (null = push not set up on the server).</summary>
    [HttpGet("config")]
    public async Task<ActionResult<AdminPushConfigDto>> Config(
        [FromServices] IConfiguration config, [FromServices] IWebPushSubscriptionRepository subs, CancellationToken ct)
    {
        var key = config["AdminAlerts:VapidPublicKey"];
        return Ok(new AdminPushConfigDto(string.IsNullOrWhiteSpace(key) ? null : key, await subs.CountForUserAsync(CurrentUserId(), ct)));
    }

    /// <summary>Turn notifications on for this device.</summary>
    [HttpPost("subscriptions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Subscribe([FromBody] AdminPushSubscribeRequest request, [FromServices] SubscribeAdminPushHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), request, Request.Headers.UserAgent.ToString(), ct);
        return NoContent();
    }

    /// <summary>Turn notifications off for this device.</summary>
    [HttpDelete("subscriptions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsubscribe([FromBody] AdminPushUnsubscribeRequest request, [FromServices] UnsubscribeAdminPushHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(request.Endpoint, ct);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
