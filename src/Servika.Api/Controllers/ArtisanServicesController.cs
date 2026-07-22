using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// The signed-in artisan's published fixed-price services ("Knotless braids —
/// ₦15,000"). These render on their public profile and are bookable directly at
/// the published price — the customer pays the moment the artisan accepts.
/// </summary>
[ApiController]
[Route("api/v1/artisan/services")]
[Authorize(Roles = "Artisan")]
public sealed class ArtisanServicesController : ControllerBase
{
    /// <summary>The caller's published fixed-price services.</summary>
    /// <response code="200">The services, alphabetical.</response>
    /// <response code="404">No artisan profile linked to this account.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ArtisanServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ArtisanServiceDto>>> GetMine(
        [FromServices] GetMyArtisanServicesHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Publish a fixed-price service (same name = revise its price).</summary>
    /// <response code="200">The published service.</response>
    /// <response code="400">Missing name / non-positive price.</response>
    /// <response code="404">No artisan profile linked to this account.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ArtisanServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtisanServiceDto>> Save(
        [FromBody] SaveArtisanServiceRequest request,
        [FromServices] SaveArtisanServiceHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), request, ct));
    }

    /// <summary>Remove one of the caller's published services.</summary>
    /// <response code="204">Removed.</response>
    /// <response code="404">Not one of the caller's services.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteArtisanServiceHandler handler,
        CancellationToken ct)
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
