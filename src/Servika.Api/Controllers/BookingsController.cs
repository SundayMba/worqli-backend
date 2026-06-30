using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Bookings;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Api.Controllers;

/// <summary>
/// Customer booking endpoints (PRD §Booking). Every action is scoped to the
/// signed-in customer — the booking happy path: create → list → detail → cancel.
/// Artisan-side transitions (accept, on-my-way, complete) land in a later slice.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/bookings")]
[Produces("application/json")]
[Tags("Bookings")]
public sealed class BookingsController : ControllerBase
{
    /// <summary>Submit a new booking for the current customer.</summary>
    /// <remarks>
    /// Mirrors the mobile booking flow (service + details, location, confirm). The
    /// booking is created in <c>Pending</c> awaiting an artisan. Pricing and
    /// commission are decided server-side.
    /// </remarks>
    /// <response code="201">Booking created (Pending).</response>
    /// <response code="400">Validation failed (e.g. missing description, bad urgency).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Unknown category slug or artisan id.</response>
    [HttpPost]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Create(
        [FromBody] CreateBookingRequest request,
        [FromServices] CreateBookingHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(CurrentUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>List the current customer's bookings (history), newest first.</summary>
    /// <remarks>Optional <c>?status=Pending</c> (case-insensitive) filter.</remarks>
    /// <response code="200">The customer's bookings.</response>
    /// <response code="400">Unknown status filter.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> GetMine(
        [FromServices] GetMyBookingsHandler handler,
        CancellationToken ct,
        [FromQuery] string? status = null)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), status, ct));
    }

    /// <summary>Get one of the current customer's bookings in full.</summary>
    /// <response code="200">The booking.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such booking owned by this customer.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> GetById(
        Guid id,
        [FromServices] GetBookingByIdHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Cancel one of the current customer's bookings.</summary>
    /// <remarks>Allowed only while the booking is Pending or Accepted.</remarks>
    /// <response code="200">Cancelled — returns the updated booking.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such booking owned by this customer.</response>
    /// <response code="409">The booking can no longer be cancelled in its state.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> Cancel(
        Guid id,
        [FromServices] CancelBookingHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Confirm one of the current customer's bookings is complete.</summary>
    /// <remarks>
    /// Closes the job: <c>InProgress → Completed</c>. Only the customer (or an
    /// admin) confirms completion — the artisan can't mark their own work done.
    /// </remarks>
    /// <response code="200">Completed — returns the updated booking.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such booking owned by this customer.</response>
    /// <response code="409">The booking is not InProgress and can't be completed.</response>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> Complete(
        Guid id,
        [FromServices] CompleteBookingHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
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
