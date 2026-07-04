using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// Artisan self-onboarding: the signed-in artisan reads and creates/updates their
/// own marketplace profile. Role-gated to Artisan. A newly onboarded profile is
/// auto-verified for now (admin KYC review lands with the admin slice) and then
/// appears in the public catalogue + can be booked.
/// </summary>
[Authorize(Roles = "Artisan")]
[ApiController]
[Route("api/v1/artisan/profile")]
[Produces("application/json")]
[Tags("Artisan Profile")]
public sealed class ArtisanProfileController : ControllerBase
{
    /// <summary>Get the current artisan's profile.</summary>
    /// <response code="200">Their profile.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an artisan account.</response>
    /// <response code="404">No profile yet — route to onboarding.</response>
    [HttpGet]
    [ProducesResponseType(typeof(MyArtisanProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyArtisanProfileDto>> GetMine(
        [FromServices] GetMyArtisanProfileHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), ct));
    }

    /// <summary>Create the current artisan's profile (onboarding).</summary>
    /// <response code="201">Profile created.</response>
    /// <response code="400">Missing/invalid fields.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">A supplied category slug does not exist.</response>
    [HttpPost]
    [ProducesResponseType(typeof(MyArtisanProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyArtisanProfileDto>> Create(
        [FromBody] SaveArtisanProfileRequest request,
        [FromServices] SaveArtisanProfileHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(CurrentUserId(), request, ct);
        return CreatedAtAction(nameof(GetMine), null, result);
    }

    /// <summary>Update the current artisan's profile.</summary>
    /// <response code="200">Profile updated.</response>
    /// <response code="400">Missing/invalid fields.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">A supplied category slug does not exist.</response>
    [HttpPut]
    [ProducesResponseType(typeof(MyArtisanProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyArtisanProfileDto>> Update(
        [FromBody] SaveArtisanProfileRequest request,
        [FromServices] SaveArtisanProfileHandler handler,
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
