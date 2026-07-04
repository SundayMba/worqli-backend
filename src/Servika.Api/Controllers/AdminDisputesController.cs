using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Disputes;
using Servika.Contracts.Disputes;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin dispute resolution (PRD §Disputes). Role-gated to Admin/SuperAdmin. The
/// admin <b>web dashboard</b> isn't built yet, but the API is complete and
/// curl-testable with the seeded admin account (<c>admin@servika.test</c>).
/// Resolving a dispute also drives its booking to a terminal state and notifies the
/// customer.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/disputes")]
[Produces("application/json")]
[Tags("Admin Disputes")]
public sealed class AdminDisputesController : ControllerBase
{
    /// <summary>The dispute queue, newest first, optionally one status only.</summary>
    /// <response code="200">The disputes.</response>
    /// <response code="400">Unknown status filter.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin account.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DisputeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DisputeDto>>> List(
        [FromQuery] string? status,
        [FromServices] ListDisputesHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(status, ct));
    }

    /// <summary>A single dispute by id.</summary>
    /// <response code="200">The dispute.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Dispute not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisputeDto>> Get(
        Guid id,
        [FromServices] GetDisputeHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, ct));
    }

    /// <summary>Acknowledge a dispute and start investigating (Open → UnderReview).</summary>
    /// <response code="200">The dispute is now under review.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Dispute not found.</response>
    /// <response code="409">Only an open dispute can be moved under review.</response>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(typeof(DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DisputeDto>> Review(
        Guid id,
        [FromServices] MarkDisputeUnderReviewHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Resolve a dispute with a decision (customer / artisan) + optional note.</summary>
    /// <remarks>Favouring the customer cancels the booking (refund follows in a later
    /// payments slice); favouring the artisan completes it. The customer is notified.</remarks>
    /// <response code="200">The dispute is resolved; the booking moved to its terminal state.</response>
    /// <response code="400">Missing/invalid outcome.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Dispute not found.</response>
    /// <response code="409">Dispute already resolved.</response>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(DisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DisputeDto>> Resolve(
        Guid id,
        [FromBody] ResolveDisputeRequest request,
        [FromServices] ResolveDisputeHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
            throw new InvalidCredentialsException();
        return userId;
    }
}
