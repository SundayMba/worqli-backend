namespace Servika.Contracts.Auth;

/// <summary>Body for POST /api/v1/auth/forgot-password.</summary>
/// <param name="EmailOrPhone">The account to start a reset for.</param>
public sealed record ForgotPasswordRequest(string EmailOrPhone);

/// <summary>Result of starting a reset. Always reports success to avoid revealing account existence.</summary>
/// <param name="ResetStarted">Always true.</param>
public sealed record ForgotPasswordResponse(bool ResetStarted);

/// <summary>Body for PATCH /api/v1/auth/reset-password.</summary>
/// <param name="EmailOrPhone">The account being reset (identifies whose code to match).</param>
/// <param name="TokenOrOtp">The 6-digit reset code from the forgot-password step.</param>
/// <param name="NewPassword">The new password (min 8 chars).</param>
/// <param name="ConfirmPassword">Must match NewPassword.</param>
public sealed record ResetPasswordRequest(
    string EmailOrPhone,
    string TokenOrOtp,
    string NewPassword,
    string ConfirmPassword);
