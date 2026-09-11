using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Payments;
using Servika.Contracts.Payments;

namespace Servika.Api.Controllers;

/// <summary>
/// The list of banks a payout can target (name + provider code), for the
/// withdrawal bank picker. Any signed-in user (artisans cashing out earnings,
/// referrers cashing out rewards) can read it.
/// </summary>
[ApiController]
[Route("api/v1/banks")]
[Authorize]
[Produces("application/json")]
public sealed class BanksController : ControllerBase
{
    /// <summary>Payout-destination banks, alphabetical.</summary>
    /// <response code="200">The banks.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BankDto>>> GetBanks(
        [FromServices] GetBanksHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Whose name is on an account. Cached per bank+number; ten lookups a day per user.</summary>
    /// <response code="404">No account with that number at that bank.</response>
    /// <response code="429">Daily cap reached.</response>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(Servika.Contracts.Payments.BankAccountResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<Servika.Contracts.Payments.BankAccountResolutionDto>> Resolve(
        [FromQuery] string bankCode,
        [FromQuery] string accountNumber,
        [FromServices] Servika.Application.Payments.ResolveBankAccountHandler handler,
        CancellationToken ct)
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        return Ok(await handler.HandleAsync(userId, bankCode, accountNumber, ct));
    }
}
