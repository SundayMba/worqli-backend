using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin user directory + moderation. Role-gated. Lists accounts and lets an admin
/// suspend (block sign-in) or reactivate them. Admin accounts can't be suspended.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/users")]
[Produces("application/json")]
[Tags("Admin Users")]
public sealed class AdminUsersController : ControllerBase
{
    /// <summary>The user directory, newest first, optional role filter
    /// (Customer / Artisan / Admin / SuperAdmin).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> List(
        [FromQuery] string? role,
        [FromServices] ListUsersHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(role, ct));
    }

    /// <summary>Suspend an account (blocks sign-in).</summary>
    /// <response code="404">User not found.</response>
    /// <response code="409">Admin accounts can't be suspended.</response>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> Suspend(
        Guid id,
        [FromServices] SetUserSuspendedHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, suspend: true, ct));
    }

    /// <summary>Reactivate a suspended account.</summary>
    /// <response code="404">User not found.</response>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDto>> Reactivate(
        Guid id,
        [FromServices] SetUserSuspendedHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, suspend: false, ct));
    }
}
