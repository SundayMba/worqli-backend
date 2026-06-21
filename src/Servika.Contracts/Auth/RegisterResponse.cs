namespace Servika.Contracts.Auth;

/// <summary>
/// What POST /api/v1/auth/register returns. Registration no longer logs the user
/// in: it creates an unverified account and emails a verification code. The app
/// then collects that code at <c>verify-otp</c>, which issues the session. So no
/// tokens are returned here — only enough to drive the "verify your email" step.
/// </summary>
/// <param name="Email">The normalised email the verification code was sent to.</param>
/// <param name="VerificationRequired">Always true — the user must verify their email next.</param>
public sealed record RegisterResponse(
    string Email,
    bool VerificationRequired = true);
