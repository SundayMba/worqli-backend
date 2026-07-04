using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Referrals;
using Servika.Contracts.Payments;
using Servika.Contracts.Referrals;

namespace Servika.Api.Controllers;

/// <summary>
/// Referral program (PRD §Referrals). The signed-in user's share code, referral
/// earnings, and the artisans they've referred. Earnings are credited when a
/// referred artisan completes their first job (see <c>ReferralService</c>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/referrals")]
[Produces("application/json")]
[Tags("Referrals")]
public sealed class ReferralsController : ControllerBase
{
    /// <summary>The current user's referral dashboard.</summary>
    /// <response code="200">Code, earnings, and referred artisans.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ReferralSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ReferralSummaryDto>> GetMine(
        [FromServices] GetMyReferralsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Cash out the referral reward pool to a bank account.</summary>
    /// <remarks>The amount is validated against the referral pool balance
    /// (ledger-computed) server-side; a ledger debit reserves the funds and the
    /// gateway disburses. Shares the artisan payout rails.</remarks>
    /// <response code="201">Payout requested (Pending) or disbursed.</response>
    /// <response code="400">Missing bank details or non-positive amount.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="409">Below the minimum, or more than the available balance.</response>
    [HttpPost("withdrawals")]
    [ProducesResponseType(typeof(WithdrawalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WithdrawalDto>> RequestWithdrawal(
        [FromBody] RequestWithdrawalRequest request,
        [FromServices] RequestReferralWithdrawalHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(CurrentUserId(), request, ct);
        return CreatedAtAction(nameof(GetMine), new { }, result);
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
