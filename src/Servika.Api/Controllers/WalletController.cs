using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Payments;
using Servika.Contracts.Payments;

namespace Servika.Api.Controllers;

/// <summary>
/// Wallet endpoints (PRD §Payments and Wallet). Balance and history are always
/// computed from the append-only ledger, never trusted from the client.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/wallet")]
[Produces("application/json")]
[Tags("Wallet")]
public sealed class WalletController : ControllerBase
{
    /// <summary>The current user's wallet balance.</summary>
    /// <response code="200">Balance in Naira.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WalletDto>> Get(
        [FromServices] GetWalletHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>The current user's wallet ledger history, newest first.</summary>
    /// <response code="200">The transactions.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionDto>>> Transactions(
        [FromServices] GetWalletTransactionsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
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
