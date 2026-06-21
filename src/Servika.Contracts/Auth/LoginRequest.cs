namespace Servika.Contracts.Auth;

/// <summary>Body for POST /api/v1/auth/login.</summary>
/// <param name="EmailOrPhone">The user's email address or phone number.</param>
/// <param name="Password">Plain password, checked against the stored hash.</param>
/// <param name="DeviceId">Optional device identifier, for per-device session tracking.</param>
public sealed record LoginRequest(
    string EmailOrPhone,
    string Password,
    string? DeviceId = null);
