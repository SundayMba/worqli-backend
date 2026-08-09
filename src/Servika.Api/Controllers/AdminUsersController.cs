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

    /// <summary>Soft-delete an account: hidden everywhere and blocked from sign-in, but
    /// recoverable for a grace period before the background purge erases it.</summary>
    /// <response code="204">Soft-deleted.</response>
    /// <response code="404">User not found.</response>
    /// <response code="409">Admin accounts can't be deleted.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] SoftDeleteUserHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    /// <summary>Restore a soft-deleted account (and its artisan profile).</summary>
    /// <response code="204">Restored.</response>
    /// <response code="404">User not found.</response>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(
        Guid id,
        [FromServices] RestoreUserHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    /// <summary>PERMANENTLY delete an account and everything tied to it (profile, KYC,
    /// bookings, ledger, reviews, chats, and all uploaded files). Irreversible.</summary>
    /// <response code="204">Erased.</response>
    /// <response code="404">User not found.</response>
    /// <response code="409">Admin accounts can't be deleted.</response>
    [HttpDelete("{id:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePermanent(
        Guid id,
        [FromServices] AdminDeleteUserHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }
}
