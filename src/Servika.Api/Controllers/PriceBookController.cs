using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Contracts.Admin;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// Servika's price book (Business Model v1.4, section 7): the prices Servika sets. Public reads for the
/// customer app (the Home rail, category screens, the job screen); admin writes for the console.
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Price book")]
public sealed class PriceBookController : ControllerBase
{
    /// <summary>Active price-book items. <c>popular=true</c> = the Home rail; <c>kind</c> = Fixed, AddOn or Custom.</summary>
    [HttpGet("api/v1/price-book")]
    [ProducesResponseType(typeof(IReadOnlyList<PriceBookItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PriceBookItemDto>>> List(
        [FromQuery] string? category, [FromQuery] string? kind, [FromQuery] bool popular,
        [FromServices] GetPriceBookHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(category, kind, popular, ct));
    }

    /// <summary>A category screen: its fixed jobs, the diagnosis fee and how many repairs it covers.</summary>
    /// <response code="404">Unknown or hidden category.</response>
    [HttpGet("api/v1/price-book/categories/{slug}")]
    [ProducesResponseType(typeof(CategoryPriceBookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryPriceBookDto>> Category(
        string slug, [FromServices] GetCategoryPriceBookHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(slug, ct));
    }

    /// <summary>One active item.</summary>
    /// <response code="404">Unknown, hidden or retired.</response>
    [HttpGet("api/v1/price-book/{id:guid}")]
    [ProducesResponseType(typeof(PriceBookItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceBookItemDto>> Get(
        Guid id, [FromServices] GetPriceBookItemHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, ct));
    }

    /// <summary>The item's cover photo (image bytes). 404 → the app shows its placeholder.</summary>
    [HttpGet("api/v1/price-book/{id:guid}/photo")]
    [ResponseCache(Duration = 86400)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Photo(
        Guid id, [FromServices] GetPriceBookPhotoHandler handler, CancellationToken ct)
    {
        var file = await handler.HandleAsync(id, ct);
        return File(file.Content, file.ContentType);
    }

    /// <summary>Every item incl. inactive, with its price signals from the last 30 days (admin).</summary>
    [HttpGet("api/v1/admin/price-book")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPriceBookItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPriceBookItemDto>>> AdminList(
        [FromServices] ListAdminPriceBookHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Add a price-book line (admin).</summary>
    /// <response code="400">Invalid item (no price on a fixed job, range upside down, ...).</response>
    /// <response code="404">Unknown category.</response>
    [HttpPost("api/v1/admin/price-book")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(AdminPriceBookItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPriceBookItemDto>> Create(
        [FromBody] SavePriceBookItemRequest request, [FromServices] SavePriceBookItemHandler handler, CancellationToken ct)
    {
        var item = await handler.HandleAsync(null, CurrentUserId(), request, ct);
        return CreatedAtAction(nameof(AdminList), new { }, item);
    }

    /// <summary>Edit a price-book line (admin). New bookings use the new price; booked jobs keep theirs.</summary>
    [HttpPut("api/v1/admin/price-book/{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(AdminPriceBookItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPriceBookItemDto>> Update(
        Guid id, [FromBody] SavePriceBookItemRequest request, [FromServices] SavePriceBookItemHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, CurrentUserId(), request, ct));
    }

    /// <summary>Upload a cover photo (admin). Returns the new photo URL.</summary>
    [HttpPut("api/v1/admin/price-book/{id:guid}/photo")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(PhotoUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhotoUrlResponse>> SetPhoto(
        Guid id, [FromBody] SetPriceBookPhotoRequest request, [FromServices] SetPriceBookPhotoHandler handler, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PhotoBase64)) throw new ArgumentException("Choose a photo.");
        return Ok(new PhotoUrlResponse(await handler.HandleAsync(id, CurrentUserId(), request.PhotoBase64, ct)));
    }

    /// <summary>Remove the cover photo (admin); the app goes back to its placeholder.</summary>
    [HttpDelete("api/v1/admin/price-book/{id:guid}/photo")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePhoto(
        Guid id, [FromServices] SetPriceBookPhotoHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, CurrentUserId(), null, ct);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}

/// <summary>Where an uploaded photo is served from.</summary>
public sealed record PhotoUrlResponse(string? PhotoUrl);
