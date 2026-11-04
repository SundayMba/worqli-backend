namespace Servika.Contracts.Auth;

/// <summary>
/// Sign in with Apple (POST /api/v1/auth/apple). <paramref name="IdentityToken"/> is the JWT from Apple's
/// sign-in sheet. Apple shares the person's name only on their very first sign-in, so the app passes it in
/// <paramref name="FullName"/> when it has it; later sign-ins send null.
/// </summary>
public sealed record AppleLoginRequest(string IdentityToken, string? FullName = null);
