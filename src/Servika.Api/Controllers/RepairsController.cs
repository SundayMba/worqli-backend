using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Bookings;
using Servika.Contracts.Bookings;

namespace Servika.Api.Controllers;

/// <summary>
/// Diagnose-and-fix (Business Model v1.4, section 7): at the job the artisan picks repairs from Servika's
/// add-on list with a photo each; the customer approves or declines them in the app before any work starts.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
[Tags("Repairs")]
public sealed class RepairsController : ControllerBase
{
    /// <summary>The artisan sends the repairs the job needs (Servika's prices, a photo each).</summary>
    /// <response code="400">Not on the list for this job, no photo, parts without a note, too many.</response>
    /// <response code="404">Not your job.</response>
    /// <response code="409">Not a job priced from the list, not at the job yet, unpaid, or a list is still waiting.</response>
    [HttpPost("api/v1/artisan/jobs/{id:guid}/repairs")]
    [Authorize(Roles = "Artisan")]
    [ProducesResponseType(typeof(BookingRepairsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingRepairsDto>> Propose(
        Guid id, [FromBody] ProposeRepairsRequest request, [FromServices] ProposeRepairsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    /// <summary>The repairs on a job and what is paid and owed (the job's customer or its artisan).</summary>
    [HttpGet("api/v1/bookings/{id:guid}/repairs")]
    [HttpGet("api/v1/artisan/jobs/{id:guid}/repairs")]
    [ProducesResponseType(typeof(BookingRepairsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingRepairsDto>> List(
        Guid id, [FromServices] GetBookingRepairsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>A repair's fault photo (the job's customer or its artisan).</summary>
    [HttpGet("api/v1/bookings/{id:guid}/repairs/{repairId:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Photo(
        Guid id, Guid repairId, [FromServices] GetRepairPhotoHandler handler, CancellationToken ct)
    {
        var file = await handler.HandleAsync(CurrentUserId(), id, repairId, ct);
        return File(file.Content, file.ContentType);
    }

    /// <summary>The customer approves the repairs to go ahead with; the rest are declined.</summary>
    /// <response code="404">Not your booking.</response>
    /// <response code="409">Nothing waiting for an answer.</response>
    [HttpPost("api/v1/bookings/{id:guid}/repairs/decision")]
    [ProducesResponseType(typeof(BookingRepairsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingRepairsDto>> Decide(
        Guid id, [FromBody] DecideRepairsRequest request, [FromServices] DecideRepairsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
