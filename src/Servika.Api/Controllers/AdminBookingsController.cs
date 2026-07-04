using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin booking oversight (read-only). Role-gated. Lists every booking across the
/// marketplace and serves the full detail of one for monitoring.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/bookings")]
[Produces("application/json")]
[Tags("Admin Bookings")]
public sealed class AdminBookingsController : ControllerBase
{
    /// <summary>Every booking, newest first, optional status filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminBookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AdminBookingDto>>> List(
        [FromQuery] string? status,
        [FromServices] ListAllBookingsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(status, ct));
    }

    /// <summary>The full detail of one booking.</summary>
    /// <response code="404">Booking not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminBookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminBookingDetailDto>> Get(
        Guid id,
        [FromServices] GetAdminBookingHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, ct));
    }
}
