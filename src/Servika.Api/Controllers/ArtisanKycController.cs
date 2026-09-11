using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// Artisan KYC — submit a selfie + one government ID for verification and read the
/// review status. Role-gated to Artisan. Reviewed manually for launch (a
/// <c>Kyc:AutoApprove</c> flag approves in dev); approval flips the artisan's
/// profile to Verified so it appears in the catalogue and can be booked.
/// </summary>
[Authorize(Roles = "Artisan")]
[ApiController]
[Route("api/v1/artisan/kyc")]
[Produces("application/json")]
[Tags("Artisan KYC")]
public sealed class ArtisanKycController : ControllerBase
{
    /// <summary>The current artisan's KYC status.</summary>
    /// <response code="200">Status ("NotSubmitted" / "Pending" / "Verified" / "Rejected").</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(KycStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<KycStatusDto>> GetStatus(
        [FromServices] GetKycStatusHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Submit (or re-submit) KYC documents.</summary>
    /// <response code="200">Submitted; returns the resulting status.</response>
    /// <response code="400">Missing/invalid images or unknown ID type.</response>
    /// <response code="401">Not signed in.</response>
    [HttpPost]
    [ProducesResponseType(typeof(KycStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<KycStatusDto>> Submit(
        [FromBody] SubmitKycRequest request,
        [FromServices] SubmitKycHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), request, ct));
    }

    /// <summary>Checks a NIN against the register and records the outcome for the reviewer.</summary>
    /// <response code="200">Matched | NameMismatch | NotFound | Failed | Unavailable, with lookups left today.</response>
    /// <response code="400">Not eleven digits.</response>
    /// <response code="404">No artisan profile yet.</response>
    /// <response code="429">Three lookups today already.</response>
    [HttpPost("nin/verify")]
    [ProducesResponseType(typeof(NinVerifyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<NinVerifyResultDto>> VerifyNin(
        [FromBody] VerifyNinRequest request,
        [FromServices] VerifyNinHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), request, ct));
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
