using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Application.Reviews;
using Servika.Contracts.Catalogue;
using Servika.Contracts.Reviews;

namespace Servika.Api.Controllers;

/// <summary>
/// Marketplace catalogue endpoints (PRD §Marketplace): browse service categories
/// and artisan profiles. All read-only and open to guests, so customers can
/// explore before signing up.
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Catalogue")]
public sealed class CatalogueController : ControllerBase
{
    /// <summary>List the active service categories, in display order.</summary>
    /// <response code="200">The category catalogue.</response>
    [HttpGet("api/v1/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(
        [FromServices] GetCategoriesHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>List artisan summaries, optionally filtered to one category.</summary>
    /// <remarks>Powers the home "Nearby Artisans" carousel (no filter) and
    /// category browsing (<c>?category={slug}</c>). When <c>lat</c> and <c>lng</c>
    /// are supplied, the list is re-sorted by real proximity to the customer and
    /// each card's distance is computed from those coordinates.</remarks>
    /// <response code="200">Matching artisan summaries.</response>
    /// <response code="404">The supplied category slug does not exist.</response>
    [HttpGet("api/v1/artisans")]
    [ProducesResponseType(typeof(IReadOnlyList<ArtisanSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ArtisanSummaryDto>>> GetArtisans(
        [FromServices] GetArtisansHandler handler,
        CancellationToken ct,
        [FromQuery] string? category = null,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        return Ok(await handler.HandleAsync(category, lat, lng, ct));
    }

    /// <summary>List artisans that serve a given category (by slug).</summary>
    /// <response code="200">Artisans in the category.</response>
    /// <response code="404">No such category.</response>
    [HttpGet("api/v1/categories/{slug}/artisans")]
    [ProducesResponseType(typeof(IReadOnlyList<ArtisanSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ArtisanSummaryDto>>> GetArtisansByCategory(
        string slug,
        [FromServices] GetArtisansHandler handler,
        CancellationToken ct,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        return Ok(await handler.HandleAsync(slug, lat, lng, ct));
    }

    /// <summary>Get a single artisan's full profile.</summary>
    /// <response code="200">The artisan profile.</response>
    /// <response code="404">No artisan with this id.</response>
    [HttpGet("api/v1/artisans/{id:guid}")]
    [ProducesResponseType(typeof(ArtisanDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtisanDetailDto>> GetArtisan(
        Guid id,
        [FromServices] GetArtisanByIdHandler handler,
        CancellationToken ct,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        return Ok(await handler.HandleAsync(id, lat, lng, ct));
    }

    /// <summary>Who an artisan's personal booking code belongs to: enough to book them, nothing to browse by.</summary>
    /// <response code="404">No verified artisan has that code.</response>
    [HttpGet("api/v1/artisans/by-code/{code}")]
    [ProducesResponseType(typeof(ArtisanByCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtisanByCodeDto>> GetArtisanByCode(
        string code, [FromServices] GetArtisanByCodeHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(code, ct));
    }

    /// <summary>The artisan's uploaded profile photo (image bytes).</summary>
    /// <remarks>The <c>photoUrl</c> field on the catalogue DTOs points here.
    /// Clients fall back to bundled art when it's null / this 404s.</remarks>
    /// <response code="200">The photo.</response>
    /// <response code="404">Unknown artisan, or no photo uploaded.</response>
    [HttpGet("api/v1/artisans/{id:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtisanPhoto(
        Guid id,
        [FromServices] GetArtisanPhotoHandler handler,
        CancellationToken ct)
    {
        var file = await handler.HandleAsync(id, cover: false, ct);
        return File(file.Content, file.ContentType);
    }

    /// <summary>The artisan's uploaded cover photo (them at work).</summary>
    /// <remarks>The <c>coverPhotoUrl</c> field on the detail DTOs points here.</remarks>
    /// <response code="200">The cover photo.</response>
    /// <response code="404">Unknown artisan, or no cover uploaded.</response>
    [HttpGet("api/v1/artisans/{id:guid}/cover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtisanCover(
        Guid id,
        [FromServices] GetArtisanPhotoHandler handler,
        CancellationToken ct)
    {
        var file = await handler.HandleAsync(id, cover: true, ct);
        return File(file.Content, file.ContentType);
    }

    /// <summary>One of the artisan's work-gallery photos (image bytes).</summary>
    /// <remarks>The <c>galleryUrls</c> entries on the detail DTO point here.</remarks>
    /// <response code="200">The photo.</response>
    /// <response code="404">Unknown artisan or photo (incl. deleted ones).</response>
    [HttpGet("api/v1/artisans/{id:guid}/gallery/{photoKey}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtisanGalleryPhoto(
        Guid id,
        string photoKey,
        [FromServices] GetArtisanGalleryPhotoHandler handler,
        CancellationToken ct)
    {
        // Signed-in callers: the owner and admins may also see a photo still waiting for ops.
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var viewer = Guid.TryParse(sub, out var uid) ? uid : (Guid?)null;
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var file = await handler.HandleAsync(id, photoKey, ct, viewer, isAdmin);
        return File(file.Content, file.ContentType);
    }

    /// <summary>An artisan's customer reviews, newest first (admin and ops only).</summary>
    /// <remarks>Ratings and reviews are for ops, never shown publicly (user decision 2026-11-04), so newly
    /// registered artisans are never starved by a star average.</remarks>
    /// <response code="200">The artisan's reviews (empty if none yet).</response>
    /// <response code="404">No artisan with this id.</response>
    [HttpGet("api/v1/artisans/{id:guid}/reviews")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> GetArtisanReviews(
        Guid id,
        [FromServices] GetArtisanReviewsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, ct));
    }
}
