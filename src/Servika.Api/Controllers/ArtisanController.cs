using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Bookings;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Api.Controllers;

/// <summary>
/// Artisan-side job endpoints. Every action requires the <c>Artisan</c> role and
/// is scoped to the jobs assigned to the signed-in artisan's profile — another
/// artisan's (or an unassigned) job is a 404, never actionable. These drive the
/// booking state machine forward: accept → on-my-way → arrived → start work.
/// (The customer confirms completion — see <c>BookingsController.Complete</c>.)
/// </summary>
[Authorize(Roles = "Artisan")]
[ApiController]
[Route("api/v1/artisan/jobs")]
[Produces("application/json")]
[Tags("Artisan")]
public sealed class ArtisanController : ControllerBase
{
    /// <summary>List the jobs assigned to the current artisan, newest first.</summary>
    /// <remarks>Optional <c>?status=Pending</c> (case-insensitive) filter — e.g. incoming requests.</remarks>
    /// <response code="200">The artisan's jobs (empty if the account has no linked profile).</response>
    /// <response code="400">Unknown status filter.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in but not an artisan.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> GetJobs(
        [FromServices] GetArtisanJobsHandler handler,
        CancellationToken ct,
        [FromQuery] string? status = null)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), status, ct));
    }

    /// <summary>Get one of the current artisan's assigned jobs in full.</summary>
    /// <response code="200">The job.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in but not an artisan.</response>
    /// <response code="404">No such job assigned to this artisan.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> GetJob(
        Guid id,
        [FromServices] GetArtisanJobByIdHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Accept a pending job (Pending → Accepted).</summary>
    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<BookingDetailDto>> Accept(
        Guid id, [FromServices] AdvanceBookingByArtisanHandler handler, CancellationToken ct) =>
        Advance(id, ArtisanBookingAction.Accept, handler, ct);

    /// <summary>Decline a pending job (Pending → Rejected).</summary>
    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<BookingDetailDto>> Reject(
        Guid id, [FromServices] AdvanceBookingByArtisanHandler handler, CancellationToken ct) =>
        Advance(id, ArtisanBookingAction.Reject, handler, ct);

    /// <summary>Start the trip to the customer (Accepted → OnMyWay).</summary>
    [HttpPost("{id:guid}/on-my-way")]
    public Task<ActionResult<BookingDetailDto>> OnMyWay(
        Guid id, [FromServices] AdvanceBookingByArtisanHandler handler, CancellationToken ct) =>
        Advance(id, ArtisanBookingAction.StartTrip, handler, ct);

    /// <summary>Mark arrival at the customer (OnMyWay → Arrived).</summary>
    [HttpPost("{id:guid}/arrive")]
    public Task<ActionResult<BookingDetailDto>> Arrive(
        Guid id, [FromServices] AdvanceBookingByArtisanHandler handler, CancellationToken ct) =>
        Advance(id, ArtisanBookingAction.Arrive, handler, ct);

    /// <summary>Begin the work (Arrived → InProgress).</summary>
    [HttpPost("{id:guid}/start")]
    public Task<ActionResult<BookingDetailDto>> Start(
        Guid id, [FromServices] AdvanceBookingByArtisanHandler handler, CancellationToken ct) =>
        Advance(id, ArtisanBookingAction.StartWork, handler, ct);

    // Shared body for the five transition verbs. A 404 means the job isn't
    // assigned to this artisan; a 409 means the move is illegal in the current
    // state (both produced by the handler/Domain and mapped in middleware).
    private async Task<ActionResult<BookingDetailDto>> Advance(
        Guid id, ArtisanBookingAction action, AdvanceBookingByArtisanHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, action, ct));
    }

    /// <summary>The signed-in user's id, taken from the validated access token.</summary>
    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
            throw new InvalidCredentialsException();

        return userId;
    }
}
