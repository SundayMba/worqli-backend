using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Payments;
using Servika.Contracts.Payments;

namespace Servika.Api.Controllers;

/// <summary>
/// Artisan earnings + payouts (PRD §Payments and Wallet). Role-gated to the
/// signed-in artisan and scoped to their profile: balance is computed from the
/// append-only ledger, and a withdrawal is validated against it server-side.
/// </summary>
[Authorize(Roles = "Artisan")]
[ApiController]
[Route("api/v1/artisan")]
[Produces("application/json")]
[Tags("Artisan Wallet")]
public sealed class ArtisanWalletController : ControllerBase
{
    /// <summary>The artisan's earnings summary (available / earned / withdrawn).</summary>
    /// <response code="200">Earnings in Naira.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an artisan account.</response>
    /// <response code="404">No artisan profile linked to this account.</response>
    [HttpGet("wallet")]
    [ProducesResponseType(typeof(ArtisanWalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtisanWalletDto>> GetWallet(
        [FromServices] GetArtisanWalletHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>The artisan's payout history, newest first.</summary>
    /// <response code="200">The withdrawals.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No artisan profile linked to this account.</response>
    [HttpGet("withdrawals")]
    [ProducesResponseType(typeof(IReadOnlyList<WithdrawalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WithdrawalDto>>> GetWithdrawals(
        [FromServices] GetArtisanWithdrawalsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Request a payout of earnings to a bank account.</summary>
    /// <remarks>The amount is validated against the artisan's available balance;
    /// a ledger debit reserves the funds and the gateway disburses.</remarks>
    /// <response code="201">Payout requested (Pending) or disbursed.</response>
    /// <response code="400">Missing bank details or non-positive amount.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No artisan profile linked to this account.</response>
    /// <response code="409">Below the minimum, or more than the available balance.</response>
    [HttpPost("withdrawals")]
    [ProducesResponseType(typeof(WithdrawalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WithdrawalDto>> RequestWithdrawal(
        [FromBody] RequestWithdrawalRequest request,
        [FromServices] RequestWithdrawalHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(CurrentUserId(), request, ct);
        return CreatedAtAction(nameof(GetWithdrawals), new { }, result);
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
