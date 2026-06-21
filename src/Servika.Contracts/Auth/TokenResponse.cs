namespace Servika.Contracts.Auth;

/// <summary>
/// What POST /api/v1/auth/refresh returns: a fresh token pair. The refresh token
/// is rotated (the old one is revoked), so always replace both stored values.
/// </summary>
/// <param name="AccessToken">New short-lived signed JWT.</param>
/// <param name="AccessTokenExpiresAtUtc">When the new access token expires (UTC).</param>
/// <param name="RefreshToken">New refresh token. The previous one is now invalid.</param>
public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken);
