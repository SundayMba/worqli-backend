using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin artisan directory — every artisan with reputation, verification, and
/// commission standing (owed cash-job fees / restricted). Role-gated to admins.
/// </summary>
[ApiController]
[Route("api/v1/admin/artisans")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Produces("application/json")]
public sealed class AdminArtisansController : ControllerBase
{
    /// <summary>The full artisan directory (pending first, then alphabetical).</summary>
    /// <response code="200">The artisans.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminArtisanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AdminArtisanDto>>> List(
        [FromServices] ListAdminArtisansHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(ct));
    }

    /// <summary>An artisan's uploaded work certificate (base64 data URI, or null).</summary>
    /// <response code="200">The certificate data URI (null if none uploaded).</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">Unknown artisan.</response>
    [HttpGet("{id:guid}/certificate")]
    [ProducesResponseType(typeof(AdminArtisanCertificateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminArtisanCertificateDto>> GetCertificate(
        Guid id,
        [FromServices] GetAdminArtisanCertificateHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, ct));
    }

    /// <summary>Waive (or restore) the guarantor requirement for one artisan.</summary>
    [HttpPost("{id:guid}/guarantor-waiver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGuarantorWaiver(
        Guid id,
        [FromBody] GuarantorWaiverRequest request,
        [FromServices] SetGuarantorWaiverHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return NoContent();
    }
}
