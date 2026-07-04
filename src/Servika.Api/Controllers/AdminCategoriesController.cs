using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin management of the marketplace catalogue's service categories. Role-gated.
/// Lists all categories (incl. hidden), creates/edits them, and toggles their
/// visibility (a soft on/off so a category can be hidden without deletion).
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/categories")]
[Produces("application/json")]
[Tags("Admin Categories")]
public sealed class AdminCategoriesController : ControllerBase
{
    /// <summary>Every category, incl. inactive, in display order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryDto>>> List(
        [FromServices] ListAllCategoriesHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Create a new category.</summary>
    /// <response code="400">Missing slug/name.</response>
    /// <response code="409">Slug already in use.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AdminCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminCategoryDto>> Create(
        [FromBody] CreateCategoryRequest request,
        [FromServices] CreateCategoryHandler handler,
        CancellationToken ct)
    {
        var category = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(List), new { }, category);
    }

    /// <summary>Edit a category's display fields (slug is immutable).</summary>
    /// <response code="404">Category not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminCategoryDto>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] UpdateCategoryHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, request, ct));
    }

    /// <summary>Show the category in the marketplace.</summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(AdminCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminCategoryDto>> Enable(
        Guid id, [FromServices] SetCategoryActiveHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, isActive: true, ct));
    }

    /// <summary>Hide the category from the marketplace (without deleting it).</summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(AdminCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminCategoryDto>> Disable(
        Guid id, [FromServices] SetCategoryActiveHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, isActive: false, ct));
    }
}
