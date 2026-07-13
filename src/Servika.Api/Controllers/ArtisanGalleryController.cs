using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// The signed-in artisan's work gallery — evidence photos of finished jobs
/// showcased on their public profile. Add after a job; delete any time.
/// </summary>
[ApiController]
[Authorize(Roles = "Artisan")]
[Route("api/v1/artisan/gallery")]
[Produces("application/json")]
[Tags("Artisan gallery")]
public sealed class ArtisanGalleryController : ControllerBase
{
    /// <summary>Add a work-evidence photo to the gallery.</summary>
    /// <response code="200">The updated gallery (photo URLs, newest first).</response>
    /// <response code="400">Missing/invalid photo, or the gallery is full.</response>
    /// <response code="404">No Pro profile yet.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GalleryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GalleryDto>> Add(
        [FromBody] AddGalleryPhotoRequest request,
        [FromServices] AddGalleryPhotoHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), request, ct));
    }

    /// <summary>Delete one gallery photo (by the key from its URL).</summary>
    /// <response code="200">The updated gallery.</response>
    /// <response code="404">Not your photo / no profile.</response>
    [HttpDelete("{photoKey}")]
    [ProducesResponseType(typeof(GalleryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GalleryDto>> Remove(
        string photoKey,
        [FromServices] RemoveGalleryPhotoHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), photoKey, ct));
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
