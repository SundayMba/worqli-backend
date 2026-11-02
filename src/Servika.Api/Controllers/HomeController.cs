using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>What the customer Home needs beyond the categories: availability and demand capture.</summary>
[ApiController]
[Produces("application/json")]
[Tags("Home")]
public sealed class HomeController : ControllerBase
{
    /// <summary>How many checked artisans could take a job at this point now, and how many cover it at all.</summary>
    [HttpGet("api/v1/availability")]
    [ProducesResponseType(typeof(AvailabilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AvailabilityDto>> GetAvailability(
        [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] string? category,
        [FromServices] GetAvailabilityHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(lat, lng, category, ct));
    }

    /// <summary>"Tell us what you need": a service or an area Servika does not offer yet. Guests may send it.</summary>
    [HttpPost("api/v1/service-interest")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordInterest(
        [FromBody] RecordServiceInterestRequest request, [FromServices] RecordServiceInterestHandler handler, CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        await handler.HandleAsync(Guid.TryParse(sub, out var id) ? id : null, request, ct);
        return NoContent();
    }

    /// <summary>The latest "tell us what you need" requests (admin).</summary>
    [HttpGet("api/v1/admin/service-interest")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceInterestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceInterestDto>>> ListInterest(
        [FromServices] ListServiceInterestsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }
}
