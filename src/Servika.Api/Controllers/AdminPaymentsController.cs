using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin payments & commissions analytics (read-only), aggregated from the wallet
/// ledger + withdrawals. Role-gated.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/payments")]
[Produces("application/json")]
[Tags("Admin Payments")]
public sealed class AdminPaymentsController : ControllerBase
{
    /// <summary>Revenue, commission, payouts, refunds, a daily revenue series, and recent transactions.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AdminPaymentsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AdminPaymentsDto>> Get(
        [FromServices] GetAdminPaymentsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }
}
