using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Tracking;
using Servika.Contracts.Tracking;

namespace Servika.Api.Controllers;

/// <summary>
/// REST companion to the tracking hub. Currently exposes the directions proxy so
/// the mobile map can draw a road-snapped route + show a real ETA without ever
/// holding the Google key (it lives in server config). Live position itself flows
/// over the SignalR hub at /hubs/tracking.
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
}
