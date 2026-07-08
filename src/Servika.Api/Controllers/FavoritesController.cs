using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Favorites;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// A customer's saved ("favourite") artisans (PRD §Marketplace). Auth-gated and
/// scoped to the signed-in user.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[Tags("Favorites")]
public sealed class FavoritesController : ControllerBase
{
    /// <summary>The current user's saved artisans, newest first.</summary>
    /// <response code="200">The saved artisans.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("favorites")]
    [ProducesResponseType(typeof(IReadOnlyList<ArtisanSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ArtisanSummaryDto>>> List(
        [FromServices] GetFavoritesHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Save an artisan to favourites (idempotent).</summary>
    /// <response code="204">Saved.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Unknown artisan.</response>
    [HttpPost("artisans/{id:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        Guid id, [FromServices] AddFavoriteHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Remove an artisan from favourites (idempotent).</summary>
    /// <response code="204">Removed.</response>
    /// <response code="401">Not signed in.</response>
    [HttpDelete("artisans/{id:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove(
        Guid id, [FromServices] RemoveFavoriteHandler handler, CancellationToken ct)
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
