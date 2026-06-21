namespace Servika.Contracts.Auth;

/// <summary>
/// What POST /api/v1/auth/login returns. Because registration is email-verified,
/// a valid password isn't enough: an account whose email is still unverified
/// can't get a session. In that case login (re)sends a verification code and
/// reports <see cref="VerificationRequired"/> = true with no <see cref="Session"/>
/// — the app then runs the same verify-otp step as sign-up. A verified account
/// gets the full <see cref="Session"/>.
/// </summary>
/// <param name="VerificationRequired">True when the email must be verified before a session is issued.</param>
/// <param name="Email">The account's email (so the app can drive the verify step).</param>
/// <param name="Session">The signed-in session (tokens + profile), or null when verification is required.</param>
public sealed record LoginResponse(
    bool VerificationRequired,
    string Email,
    AuthResponse? Session = null);
