namespace Servika.Contracts.Auth;

/// <summary>Update the signed-in user's editable profile (PATCH /api/v1/auth/me).</summary>
public sealed record UpdateProfileRequest(string FullName, string PhoneNumber);
