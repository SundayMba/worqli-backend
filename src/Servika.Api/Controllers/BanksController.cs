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
}
