namespace Servika.Contracts.Auth;

/// <summary>Body for POST /api/v1/auth/logout.</summary>
/// <param name="RefreshToken">The refresh token to revoke. Idempotent — revoking twice is harmless.</param>
/// <param name="DeviceId">Optional device identifier whose session is ending.</param>
public sealed record LogoutRequest(
    string RefreshToken,
    string? DeviceId = null);
