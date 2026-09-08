using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// The people who vouch for the signed-in artisan (verification hub, "Two
/// guarantors"). Servika contacts them only if something goes wrong on a job.
/// </summary>
[Authorize(Roles = "Artisan")]
[ApiController]
[Route("api/v1/artisan/guarantors")]
[Produces("application/json")]
[Tags("Artisan Profile")]
public sealed class ArtisanGuarantorsController : ControllerBase
{
    /// <summary>The artisan's guarantors, oldest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GuarantorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GuarantorDto>>> List(
        [FromServices] ListGuarantorsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Add a guarantor (max four). The ID photo is optional.</summary>
    /// <response code="201">Added.</response>
    /// <response code="400">Missing name, unreachable phone, or a bad image.</response>
    /// <response code="409">Already four guarantors.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GuarantorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GuarantorDto>> Add(
        [FromBody] AddGuarantorRequest request,
        [FromServices] AddGuarantorHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(CurrentUserId(), request, ct);
        return CreatedAtAction(nameof(List), null, result);
    }

    /// <summary>Remove one of your guarantors.</summary>
    /// <response code="204">Removed.</response>
    /// <response code="404">Not yours / unknown.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        Guid id, [FromServices] RemoveGuarantorHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), id, ct);
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
