using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Servika.Api.Hubs;
using Servika.Application.Common;
using Servika.Application.Tracking;
using Servika.Contracts.Tracking;

namespace Servika.Api.Controllers;

/// <summary>
/// REST companion to the tracking hub: the directions proxy (route + ETA, Google
/// key stays server-side) and a <b>fallback path for the live position itself</b> —
/// the artisan can POST pings and the customer can GET the latest position when
/// the SignalR socket is down (flaky networks/tunnels drop long-lived WebSockets).
/// The hub remains the fast path; these endpoints share its authorization rules.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/tracking")]
[Produces("application/json")]
[Tags("Tracking")]
public sealed class TrackingController : ControllerBase
{
    /// <summary>A driving route + ETA between two points (e.g. artisan → customer).</summary>
    /// <remarks>
    /// Backed by Google Directions when a server key is configured, otherwise a
    /// straight-line stub (<c>provider:"stub"</c>). The client throttles how often
    /// it calls this as the artisan moves.
    /// </remarks>
    /// <response code="200">The route polyline, distance (m) and duration (s).</response>
    /// <response code="400">A coordinate is out of range.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("route")]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RouteResponse>> GetRoute(
        [FromQuery] double fromLat,
        [FromQuery] double fromLng,
        [FromQuery] double toLat,
        [FromQuery] double toLng,
        [FromServices] GetRouteHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(fromLat, fromLng, toLat, toLng, ct));
    }

    /// <summary>The artisan's latest recorded position for a booking (REST fallback
    /// for when the customer's hub socket is down). Participants only.</summary>
    /// <response code="200">The latest position.</response>
    /// <response code="204">No active session / no fix yet.</response>
    /// <response code="403">Not a participant of this booking.</response>
    [HttpGet("bookings/{id:guid}/latest")]
    [ProducesResponseType(typeof(LocationUpdate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LocationUpdate>> GetLatest(
        Guid id,
        [FromServices] TrackingService tracking,
        CancellationToken ct)
    {
        var update = await tracking.GetLatestAsync(CurrentUserId(), id, ct);
        return update is null ? NoContent() : Ok(update);
    }

    /// <summary>Record a live-location ping over REST (the artisan's fallback when
    /// their hub socket is down). Same rules as the hub: assigned artisan only,
    /// booking must be OnMyWay. Broadcasts to the booking's tracking group.</summary>
    /// <response code="204">Recorded (and broadcast to watchers).</response>
    /// <response code="403">Not the assigned artisan / booking not OnMyWay.</response>
    [HttpPost("bookings/{id:guid}/ping")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ping(
        Guid id,
        [FromBody] TrackingPingRequest request,
        [FromServices] TrackingService tracking,
        [FromServices] IHubContext<TrackingHub> hub,
        CancellationToken ct)
    {
        var result = await tracking.RecordLocationAsync(
            CurrentUserId(), id,
            request.Latitude, request.Longitude,
            request.Accuracy, request.Heading, request.Speed, ct);

        // Keep hub watchers live even though this ping arrived over REST.
        var group = $"booking:{id}";
        if (result.Started)
            await hub.Clients.Group(group).SendAsync("TrackingStarted", new { bookingId = id }, ct);
        await hub.Clients.Group(group).SendAsync("LocationUpdated", result.Update, ct);

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
