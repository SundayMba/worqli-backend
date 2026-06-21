using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Users.Login;
using Servika.Application.Users.Logout;
using Servika.Application.Users.Me;
using Servika.Application.Users.Otp;
using Servika.Application.Users.Password;
using Servika.Application.Users.Refresh;
using Servika.Application.Users.Register;
using Servika.Contracts.Auth;

namespace Servika.Api.Controllers;

/// <summary>
/// Authentication and session endpoints (PRD §9.1). Handles account creation,
/// login, token refresh/rotation, logout, and the current-user profile.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[Tags("Auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Create a new customer or artisan account.</summary>
    /// <remarks>
    /// Creates an unverified account and emails a 6-digit verification code.
    /// Registration does NOT log the user in — the app submits that code to
    /// <c>verify-otp</c>, which issues the session. Role defaults to
    /// <c>Customer</c>; admin roles cannot be self-registered. Email must be unique.
    /// </remarks>
    /// <response code="201">Account created; verification code emailed.</response>
    /// <response code="400">Validation failed (e.g. password too short, bad role).</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        [FromServices] RegisterUserHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Authenticate a user.</summary>
    /// <remarks>
    /// Accepts email or phone in <c>emailOrPhone</c>. On success returns a
    /// <c>Session</c> (tokens + profile). If the account's email is still
    /// unverified, returns <c>verificationRequired: true</c> with no session and
    /// (re)sends a verification code — the app then runs the verify-otp step.
    /// </remarks>
    /// <response code="200">Authenticated — session, or verificationRequired.</response>
    /// <response code="401">Invalid email/phone or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginUserHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(request, ct));
    }

    /// <summary>Exchange a refresh token for a new token pair (with rotation).</summary>
    /// <remarks>
    /// The presented refresh token is revoked and a brand-new pair is issued.
    /// Always replace both stored tokens with the response. Called automatically
    /// by the mobile API client when an access token expires.
    /// </remarks>
    /// <response code="200">New token pair issued.</response>
    /// <response code="401">Refresh token unknown, revoked, or expired.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        [FromBody] RefreshRequest request,
        [FromServices] RefreshTokenHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(request, ct));
    }

    /// <summary>Invalidate a refresh token / end a device session.</summary>
    /// <remarks>Idempotent: an unknown or already-revoked token still returns success.</remarks>
    /// <response code="200">Logged out (refresh token revoked).</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        [FromServices] LogoutHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(request, ct);
        return Ok(new { success = true });
    }

    /// <summary>Verify an email/phone OTP.</summary>
    /// <remarks>
    /// For <c>account_verification</c>: marks the account verified and returns a
    /// signed-in session. For <c>password_reset</c>: confirms the code is valid
    /// (the actual change happens at reset-password).
    /// </remarks>
    /// <response code="200">Code accepted; <c>verified=true</c>.</response>
    /// <response code="400">Code invalid, expired, or already used.</response>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(VerifyOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VerifyOtpResponse>> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        [FromServices] VerifyOtpHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(request, ct));
    }

    /// <summary>Resend a one-time code, subject to a per-account cooldown.</summary>
    /// <remarks>Always reports success so account existence is never revealed.</remarks>
    /// <response code="200">Request accepted; a code is sent if the account exists.</response>
    /// <response code="429">Asked again too soon — wait before retrying.</response>
    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ResendOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResendOtpResponse>> ResendOtp(
        [FromBody] ResendOtpRequest request,
        [FromServices] ResendOtpHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(request, ct));
    }

    /// <summary>Start a password reset.</summary>
    /// <remarks>If the account exists, a reset token is sent. Always returns success.</remarks>
    /// <response code="200"><c>resetStarted=true</c>.</response>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] ForgotPasswordHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(request, ct));
    }

    /// <summary>Set a new password using a reset token from forgot-password.</summary>
    /// <remarks>Requires the token, the new password, and its confirmation (min 8 chars).</remarks>
    /// <response code="200">Password changed.</response>
    /// <response code="400">Token invalid/expired, or passwords don't match / too weak.</response>
    [HttpPatch("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        [FromServices] ResetPasswordHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(request, ct);
        return Ok(new { success = true });
    }

    /// <summary>Return the authenticated user's profile, roles, and permissions.</summary>
    /// <remarks>Requires a valid access token. Called on app boot to restore the session.</remarks>
    /// <response code="200">The current user's session profile.</response>
    /// <response code="401">Missing, invalid, or expired access token.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me(
        [FromServices] GetMeHandler handler,
        CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
            throw new InvalidCredentialsException();

        return Ok(await handler.HandleAsync(userId, ct));
    }
}
