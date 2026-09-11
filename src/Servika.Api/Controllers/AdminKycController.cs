using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Admin;
using Servika.Contracts.Admin;

namespace Servika.Api.Controllers;

/// <summary>
/// Admin KYC / document onboarding verification. Role-gated. Lists submissions,
/// serves the selfie + ID images for a human to eyeball, and applies the decision —
/// which flips both the KYC record and the artisan's profile (a Verified profile
/// appears in the catalogue and can be booked).
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/v1/admin/kyc")]
[Produces("application/json")]
[Tags("Admin KYC")]
public sealed class AdminKycController : ControllerBase
{
    /// <summary>The KYC verification queue, newest first, optional status filter
    /// (Pending / Verified / Rejected).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<KycSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<KycSubmissionDto>>> List(
        [FromQuery] string? status,
        [FromServices] ListKycSubmissionsHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(status, ct));
    }

    /// <summary>A single submission with its selfie + ID images inlined for review.</summary>
    /// <response code="404">Submission not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(KycSubmissionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KycSubmissionDetailDto>> Get(
        Guid id,
        [FromServices] GetKycSubmissionHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(id, ct));
    }

    /// <summary>Approve a submission → the artisan is Verified and listed.</summary>
    /// <response code="404">Submission not found.</response>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(KycSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KycSubmissionDto>> Approve(
        Guid id,
        [FromServices] ReviewKycHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, approve: true, reason: null, check: null, reasonCode: null, ct));
    }

    /// <summary>Reject a submission with a reason → the artisan stays off the catalogue.</summary>
    /// <response code="404">Submission not found.</response>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(KycSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KycSubmissionDto>> Reject(
        Guid id,
        [FromBody] RejectKycRequest request,
        [FromServices] ReviewKycHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, approve: false, reason: request.Reason, check: request.Check, reasonCode: request.ReasonCode, ct));
    }

    /// <summary>Ask the artisan to fix one check. The application stays Pending; only that check reopens.</summary>
    [HttpPost("{id:guid}/request-changes")]
    [ProducesResponseType(typeof(KycSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KycSubmissionDto>> RequestChanges(
        Guid id,
        [FromBody] RequestChangesRequest request,
        [FromServices] RequestKycChangesHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, request, ct));
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var userId) ? userId : Guid.Empty;
    }
}
