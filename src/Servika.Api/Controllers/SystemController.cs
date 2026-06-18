using Microsoft.AspNetCore.Mvc;

namespace Servika.Api.Controllers;

/// <summary>
/// Service health and connectivity endpoints. These are unauthenticated and
/// exist so the mobile app, load balancers, and engineers can confirm the API
/// is reachable. Business endpoints (auth, bookings, ...) get their own
/// controllers in later slices.
/// </summary>
[ApiController]
[Tags("System")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    /// <summary>Root status endpoint.</summary>
    /// <remarks>
    /// Returns a small object identifying the service. Useful as a quick
    /// "is anything listening on this host?" check.
    /// </remarks>
    /// <response code="200">The API is running.</response>
    [HttpGet("/")]
    [ProducesResponseType(typeof(ServiceStatusResponse), StatusCodes.Status200OK)]
    public IActionResult Root() =>
        Ok(new ServiceStatusResponse("Servika API", "ok"));

    /// <summary>Connectivity ping.</summary>
    /// <remarks>
    /// Lightweight endpoint the mobile app calls to confirm it can reach the
    /// API over the network. Always returns <c>pong</c>.
    /// </remarks>
    /// <response code="200">Connectivity confirmed.</response>
    [HttpGet("/api/v1/ping")]
    [ProducesResponseType(typeof(PingResponse), StatusCodes.Status200OK)]
    public IActionResult Ping() =>
        Ok(new PingResponse("pong"));
}

/// <summary>Identifies the running service.</summary>
/// <param name="Service">Human-readable service name.</param>
/// <param name="Status">Coarse status string, e.g. <c>ok</c>.</param>
public record ServiceStatusResponse(string Service, string Status);

/// <summary>Response for the connectivity ping.</summary>
/// <param name="Message">Always <c>pong</c>.</param>
public record PingResponse(string Message);
