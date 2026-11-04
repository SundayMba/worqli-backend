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

    /// <summary>An estate or facility manager says what they need; ops follow up. Guests may send it.</summary>
    [HttpPost("api/v1/estate-enquiries")]
    [ProducesResponseType(typeof(EstateEnquiryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EstateEnquiryDto>> SubmitEstateEnquiry(
        [FromBody] SubmitEstateEnquiryRequest request, [FromServices] SubmitEstateEnquiryHandler handler, CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var e = await handler.HandleAsync(Guid.TryParse(sub, out var id) ? id : null, request, ct);
        return StatusCode(StatusCodes.Status201Created, e);
    }

    /// <summary>Estate enquiries, new first (admin).</summary>
    [HttpGet("api/v1/admin/estate-enquiries")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<EstateEnquiryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EstateEnquiryDto>>> ListEstateEnquiries(
        [FromServices] ListEstateEnquiriesHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Where the follow-up is, and ops' note (admin).</summary>
    [HttpPut("api/v1/admin/estate-enquiries/{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(EstateEnquiryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstateEnquiryDto>> UpdateEstateEnquiry(
        Guid id, [FromBody] UpdateEstateEnquiryRequest request, [FromServices] UpdateEstateEnquiryHandler handler, CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Ok(await handler.HandleAsync(Guid.TryParse(sub, out var uid) ? uid : Guid.Empty, id, request, ct));
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
