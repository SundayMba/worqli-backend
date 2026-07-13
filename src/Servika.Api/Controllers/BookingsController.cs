using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Bookings;
using Servika.Application.Common;
using Servika.Application.Disputes;
using Servika.Application.Reviews;
using Servika.Contracts.Bookings;
using Servika.Contracts.Disputes;
using Servika.Contracts.Reviews;

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
    /// <summary>The price offers artisans placed on this open request.</summary>
    /// <response code="200">Bids, cheapest first (empty if none yet).</response>
    /// <response code="404">Not the caller's booking.</response>
    [HttpGet("{id:guid}/bids")]
    [ProducesResponseType(typeof(IReadOnlyList<Contracts.Bookings.BidDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Contracts.Bookings.BidDto>>> GetBids(
        Guid id,
        [FromServices] GetBookingBidsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Accept one bid — assigns that artisan at their offered price.</summary>
    /// <response code="200">The booking, now Accepted with the winning artisan.</response>
    /// <response code="404">Not the caller's booking / unknown bid.</response>
    /// <response code="409">The request is no longer open.</response>
    [HttpPost("{id:guid}/bids/{bidId:guid}/accept")]
    [ProducesResponseType(typeof(Contracts.Bookings.BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Contracts.Bookings.BookingDetailDto>> AcceptBid(
        Guid id,
        Guid bidId,
        [FromServices] AcceptBidHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, bidId, ct));
    }

    /// <summary>A job photo / the job video (image or video bytes).</summary>
    /// <remarks>Visible to the booking's owner, the assigned artisan, and — while
    /// the request is open — verified artisans in its category (bidding context).</remarks>
    /// <response code="200">The media file.</response>
    /// <response code="404">Unknown media, or the caller may not see it.</response>
    [HttpGet("{id:guid}/media/{mediaKey}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedia(
        Guid id,
        string mediaKey,
        [FromServices] GetBookingMediaHandler handler,
        CancellationToken ct)
    {
        var file = await handler.HandleAsync(CurrentUserId(), id, mediaKey, ct);
        return File(file.Content, file.ContentType);
    }

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

    /// <summary>The artisan's proof-of-work for one of the customer's bookings.</summary>
    /// <remarks>Note + photos (base64 data URIs) submitted when the artisan marked
    /// the job done — powers the "review the work & confirm" screen.</remarks>
    /// <response code="200">The completion proof (empty photos if none submitted).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such booking owned by this customer.</response>
    [HttpGet("{id:guid}/completion")]
    [ProducesResponseType(typeof(JobCompletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobCompletionDto>> GetCompletion(
        Guid id,
        [FromServices] GetJobCompletionHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Leave a review on one of the current customer's completed bookings.</summary>
    /// <remarks>
    /// Rates the booking's artisan (1–5 stars + optional comment). Allowed once,
    /// only on a <c>Completed</c> booking that had an assigned artisan. The
    /// artisan's rating is updated as part of the same transaction.
    /// </remarks>
    /// <response code="201">Review submitted.</response>
    /// <response code="400">Rating out of range (must be 1–5).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such booking owned by this customer.</response>
    /// <response code="409">Not completed, no artisan to review, or already reviewed.</response>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> SubmitReview(
        Guid id,
        [FromBody] SubmitReviewRequest request,
        [FromServices] SubmitReviewHandler handler,
        CancellationToken ct)
    {
        var review = await handler.HandleAsync(CurrentUserId(), id, request, ct);
        return CreatedAtAction(nameof(GetBookingReview), new { id }, review);
    }

    /// <summary>Get the review the current customer left for a booking, if any.</summary>
    /// <response code="200">The review.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">This booking hasn't been reviewed by the customer.</response>
    [HttpGet("{id:guid}/review")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetBookingReview(
        Guid id,
        [FromServices] GetBookingReviewHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Raise a dispute about a booking (poor work, no-show, payment, …).</summary>
    /// <remarks>Allowed once work has happened (InProgress / AwaitingConfirmation /
    /// Completed); freezes the booking in <c>Disputed</c> for an admin to resolve.</remarks>
    /// <response code="201">Dispute raised; booking is now Disputed.</response>
    /// <response code="400">Missing category or description.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Booking not found for this customer.</response>
    /// <response code="409">Booking can't be disputed, or already has an open dispute.</response>
    [HttpPost("{id:guid}/dispute")]
    [ProducesResponseType(typeof(DisputeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DisputeDto>> RaiseDispute(
        Guid id,
        [FromBody] RaiseDisputeRequest request,
        [FromServices] RaiseDisputeHandler handler,
        CancellationToken ct)
    {
        var dispute = await handler.HandleAsync(CurrentUserId(), id, request, ct);
        return CreatedAtAction(nameof(GetBookingDispute), new { id }, dispute);
    }

    /// <summary>Get the dispute the current customer raised for a booking, if any.</summary>
    /// <response code="200">The dispute.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No dispute has been raised for this booking.</response>
    [HttpGet("{id:guid}/dispute")]
    [ProducesResponseType(typeof(DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisputeDto>> GetBookingDispute(
        Guid id,
        [FromServices] GetBookingDisputeHandler handler,
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
