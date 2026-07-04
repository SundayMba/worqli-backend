using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Settings;
using Servika.Contracts.Settings;

namespace Servika.Api.Controllers;

/// <summary>
/// Platform settings the admin controls (commission, payout floor, referral reward,
/// auto-confirm window). Role-gated; the values are read at runtime by the booking,
/// payout and completion flows, so changes take effect without a redeploy.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/settings")]
[Produces("application/json")]
[Tags("Admin Settings")]
public sealed class AdminSettingsController : ControllerBase
{
    /// <summary>The current platform settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PlatformSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlatformSettingsDto>> Get(
        [FromServices] GetPlatformSettingsHandler handler, CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>Update the platform settings.</summary>
    /// <response code="200">Updated settings.</response>
    /// <response code="400">A value is out of range (e.g. commission not 0–1).</response>
    [HttpPut]
    [ProducesResponseType(typeof(PlatformSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlatformSettingsDto>> Update(
        [FromBody] UpdatePlatformSettingsRequest request,
        [FromServices] UpdatePlatformSettingsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(request, ct));
    }
}
