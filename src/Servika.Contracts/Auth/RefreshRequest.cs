namespace Servika.Contracts.Auth;

/// <summary>Body for POST /api/v1/auth/refresh.</summary>
/// <param name="RefreshToken">The current refresh token to exchange for a new token pair.</param>
public sealed record RefreshRequest(string RefreshToken);
