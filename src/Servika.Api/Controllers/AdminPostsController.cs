using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Ops review of what artisans publish (user decision 2026-11-04): every service post, cover photo included, and
/// every recent-work photo is checked before customers see it. Covers must show the artisan at work or the work
/// itself, never a phone number, office or shop address.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Produces("application/json")]
[Tags("Admin posts")]
public sealed class AdminPostsController : ControllerBase
{
    /// <summary>Service posts, waiting ones first; <c>status</c> = Pending, Approved or Rejected.</summary>
    [HttpGet("api/v1/admin/service-posts")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminServicePostDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminServicePostDto>>> ListPosts(
        [FromQuery] string? status, [FromServices] ListServicePostsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(status, ct));
    }

    /// <summary>Pass a post (customers see it) or reject it with a note for the artisan.</summary>
    [HttpPost("api/v1/admin/service-posts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DecidePost(
        Guid id, [FromBody] DecidePostRequest request, [FromServices] DecideServicePostHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), id, request, ct);
        return NoContent();
    }

    /// <summary>Recent-work photos waiting for a check, images inline.</summary>
    [HttpGet("api/v1/admin/gallery-photos")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminGalleryPhotoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminGalleryPhotoDto>>> ListPhotos(
        [FromServices] ListPendingGalleryPhotosHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Pass a recent-work photo or reject it (deleted, the artisan told why).</summary>
    [HttpPost("api/v1/admin/artisans/{artisanId:guid}/gallery/{photoKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DecidePhoto(
        Guid artisanId, string photoKey, [FromBody] DecidePostRequest request,
        [FromServices] DecideGalleryPhotoHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(artisanId, photoKey, request, ct);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
