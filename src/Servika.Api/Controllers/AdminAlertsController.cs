using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;

namespace Servika.Api.Controllers;

/// <summary>Admin alert channels (Telegram + ops email).</summary>
[ApiController]
[Route("api/v1/admin/alerts")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Produces("application/json")]
public sealed class AdminAlertsController : ControllerBase
{
    private readonly AdminAlertSweepHandler _sweep;

    public AdminAlertsController(AdminAlertSweepHandler sweep) => _sweep = sweep;

    /// <summary>Send the morning summary now. Doubles as a check that the channels work.</summary>
    [HttpPost("digest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SendDigest(CancellationToken ct)
    {
        await _sweep.SendDigestAsync(ct);
        return Accepted();
    }
}
