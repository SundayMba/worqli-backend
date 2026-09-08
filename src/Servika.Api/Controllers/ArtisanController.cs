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

    /// <summary>List open (unassigned) requests the current artisan can claim — the
    /// ones in their service categories, newest first.</summary>
    /// <response code="200">Open jobs (empty if the account has no verified profile).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in but not an artisan.</response>
    [HttpGet("open")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> GetOpenJobs(
        [FromServices] GetOpenJobsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Claim an open request (Open → Accepted, assigned to you).
    /// First-come-first-served — only one artisan wins.</summary>
    /// <response code="200">Claimed; the job is now yours (Accepted).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in but not an artisan.</response>
    /// <response code="404">No such job.</response>
    /// <response code="409">Already taken, not in your categories, or your profile isn't verified.</response>
    /// <summary>Accept the customer's counter-offer on your quote: the price is agreed
    /// at their number and the job proceeds as if they had accepted your quote.</summary>
    /// <response code="200">The booking, now at the agreed price.</response>
    /// <response code="404">You haven't quoted here.</response>
    /// <response code="409">No pending counter, or the request is no longer open.</response>
    [HttpPost("{id:guid}/bid/counter/accept")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> AcceptCounter(
        Guid id, [FromServices] RespondToCounterHandler handler, CancellationToken ct)
    {
        return Ok(await handler.AcceptAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Decline the customer's counter-offer; your quote stands.</summary>
    /// <response code="200">Your bid, with the counter cleared.</response>
    /// <response code="404">You haven't quoted here.</response>
    /// <response code="409">No pending counter.</response>
    [HttpPost("{id:guid}/bid/counter/decline")]
    [ProducesResponseType(typeof(Contracts.Bookings.BidDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Contracts.Bookings.BidDto>> DeclineCounter(
        Guid id, [FromServices] RespondToCounterHandler handler, CancellationToken ct)
    {
        return Ok(await handler.DeclineAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Ask the customer to release part of the agreed MATERIALS money from
    /// the paid escrow now, to buy parts. Capped at the itemised materials total; the
    /// customer must approve before anything moves.</summary>
    /// <response code="200">Request recorded; the customer has been notified.</response>
    /// <response code="400">Amount out of range.</response>
    /// <response code="404">Not your job.</response>
    /// <response code="409">Job not paid into escrow, no itemised materials, or already advanced.</response>
    [HttpPost("{id:guid}/materials-advance")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> RequestMaterialsAdvance(
        Guid id,
        [FromBody] Contracts.Bookings.RequestMaterialsAdvanceRequest request,
        [FromServices] RequestMaterialsAdvanceHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    /// <summary>Place (or revise) a price offer — a bid on an open bidding
    /// request, or your quote on a direct request assigned to you.</summary>
    /// <response code="200">Your current bid.</response>
    /// <response code="400">Invalid amount.</response>
    /// <response code="404">Unknown request (or a direct request that isn't yours).</response>
    /// <response code="409">Not open / not bidding-mode / not your category / unverified.</response>
    [HttpPost("{id:guid}/bid")]
    [ProducesResponseType(typeof(Contracts.Bookings.BidDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Contracts.Bookings.BidDto>> SubmitBid(
        Guid id,
        [FromBody] Contracts.Bookings.SubmitBidRequest request,
        [FromServices] SubmitBidHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    /// <summary>The caller's own bid on a request (404 = not bid yet).</summary>
    [HttpGet("{id:guid}/bid")]
    [ProducesResponseType(typeof(Contracts.Bookings.BidDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Contracts.Bookings.BidDto>> GetMyBid(
        Guid id,
        [FromServices] GetMyBidHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> ClaimJob(
        Guid id,
        [FromServices] ClaimOpenJobHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>One OPEN request in full, for deciding whether to claim or quote it.</summary>
    /// <response code="200">The request (with customer name + completed-job count).</response>
    /// <response code="404">Not open, not in your categories, or you are not verified.</response>
    [HttpGet("open/{id:guid}")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> GetOpenJob(
        Guid id,
        [FromServices] GetOpenJobDetailHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>The proof-of-work you sent for one of your jobs (note + photos as data URIs).</summary>
    /// <response code="200">The completion proof (empty photos if none submitted).</response>
    /// <response code="404">No such job assigned to this artisan.</response>
    [HttpGet("{id:guid}/completion")]
    [ProducesResponseType(typeof(JobCompletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobCompletionDto>> GetCompletion(
        Guid id,
        [FromServices] GetArtisanJobCompletionHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
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

    /// <summary>Cancel an accepted/en-route job (Accepted/OnMyWay/Arrived → Cancelled).</summary>
    /// <remarks>Any escrow the customer paid is refunded in full; the customer is
    /// notified so they can book someone else. Not allowed once work is InProgress.</remarks>
    /// <response code="200">Cancelled; escrow (if paid) refunded.</response>
    /// <response code="404">Job not assigned to this artisan.</response>
    /// <response code="409">Too late — work is already in progress.</response>
    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<BookingDetailDto>> Cancel(
        Guid id, [FromServices] AdvanceBookingByArtisanHandler handler, CancellationToken ct) =>
        Advance(id, ArtisanBookingAction.Cancel, handler, ct);

    /// <summary>Submit proof of completed work (InProgress → AwaitingConfirmation).</summary>
    /// <remarks>At least one photo is required. The customer is notified to review &
    /// confirm; it auto-confirms after the window if they don't.</remarks>
    /// <response code="200">Submitted; booking now AwaitingConfirmation.</response>
    /// <response code="400">No photos / invalid image.</response>
    /// <response code="404">Job not assigned to this artisan.</response>
    /// <response code="409">Job isn't InProgress.</response>
    [HttpPost("{id:guid}/submit-completion")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> SubmitCompletion(
        Guid id,
        [FromBody] SubmitCompletionRequest request,
        [FromServices] SubmitJobCompletionHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    /// <summary>View the dispute a customer raised on one of the artisan's jobs.</summary>
    /// <response code="200">The dispute.</response>
    /// <response code="404">Job not assigned to this artisan, or no dispute raised.</response>
    [HttpGet("{id:guid}/dispute")]
    [ProducesResponseType(typeof(Contracts.Disputes.DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Contracts.Disputes.DisputeDto>> GetDispute(
        Guid id,
        [FromServices] Application.Disputes.GetArtisanDisputeHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Respond to a dispute on one of the artisan's jobs (their side of it).</summary>
    /// <response code="200">Response saved.</response>
    /// <response code="400">Empty response.</response>
    /// <response code="404">Job not assigned to this artisan, or no dispute raised.</response>
    /// <response code="409">The dispute is already resolved.</response>
    [HttpPost("{id:guid}/dispute/respond")]
    [ProducesResponseType(typeof(Contracts.Disputes.DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Contracts.Disputes.DisputeDto>> RespondToDispute(
        Guid id,
        [FromBody] Contracts.Disputes.RespondToDisputeRequest request,
        [FromServices] Application.Disputes.RespondToDisputeHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

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
