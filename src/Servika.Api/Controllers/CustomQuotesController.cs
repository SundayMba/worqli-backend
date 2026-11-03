using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Bookings;
using Servika.Contracts.Admin;
using Servika.Contracts.Bookings;

namespace Servika.Api.Controllers;

/// <summary>
/// Custom quotes (Business Model v1.4, section 10): work outside the price book. After the paid visit the artisan
/// quotes (the existing quote endpoint); a large quote, or labour far above the typical range, waits for ops to
/// check it before the customer sees it. The customer accepts it, or declines and pays only the visit.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
[Tags("Custom quotes")]
public sealed class CustomQuotesController : ControllerBase
{
    /// <summary>The customer does not go ahead: pays only the visit, which the artisan keeps; the job closes.</summary>
    /// <response code="409">Not a custom job at the visit, or a quote was already accepted.</response>
    [HttpPost("api/v1/bookings/{id:guid}/quote/decline")]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDetailDto>> Decline(
        Guid id, [FromServices] DeclineCustomQuoteHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Custom quotes waiting for ops, oldest first (admin).</summary>
    [HttpGet("api/v1/admin/quote-reviews")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminQuoteReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminQuoteReviewDto>>> List(
        [FromServices] ListQuoteReviewsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Pass a quote (the customer sees it) or send it back with a note (the artisan revises) (admin).</summary>
    /// <response code="400">Bad outcome, or a send-back without a note.</response>
    /// <response code="409">The quote is no longer open or not waiting for a check.</response>
    [HttpPost("api/v1/admin/quote-reviews/{bidId:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decide(
        Guid bidId, [FromBody] DecideQuoteReviewRequest request, [FromServices] DecideQuoteReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(CurrentUserId(), bidId, request, ct);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
