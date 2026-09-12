namespace Servika.Contracts.Auth;

/// <summary>Change the signed-in user's own password (POST /api/v1/auth/change-password).
/// The current password is re-checked so a stolen access token alone cannot lock the owner out.</summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);
