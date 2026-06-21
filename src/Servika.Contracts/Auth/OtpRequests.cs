namespace Servika.Contracts.Auth;

/// <summary>Body for POST /api/v1/auth/verify-otp.</summary>
/// <param name="OtpCode">The code the user received.</param>
/// <param name="OtpType">"account_verification" or "password_reset".</param>
/// <param name="EmailOrPhone">The account the code was sent to.</param>
public sealed record VerifyOtpRequest(string OtpCode, string OtpType, string EmailOrPhone);

/// <summary>Result of verifying an OTP. Tokens + user are present only when verification logs the user in.</summary>
/// <param name="Verified">True if the code was valid.</param>
/// <param name="AccessToken">A new access token (account verification only); otherwise null.</param>
/// <param name="AccessTokenExpiresAtUtc">Access token expiry, when a token is issued.</param>
/// <param name="RefreshToken">A new refresh token (account verification only); otherwise null.</param>
/// <param name="User">The signed-in user's public profile (account verification only); otherwise null.</param>
public sealed record VerifyOtpResponse(
    bool Verified,
    string? AccessToken = null,
    DateTimeOffset? AccessTokenExpiresAtUtc = null,
    string? RefreshToken = null,
    UserDto? User = null);

/// <summary>Body for POST /api/v1/auth/resend-otp.</summary>
/// <param name="EmailOrPhone">The account to resend to.</param>
/// <param name="OtpType">"account_verification" or "password_reset".</param>
public sealed record ResendOtpRequest(string EmailOrPhone, string OtpType);

/// <summary>Result of a resend request.</summary>
/// <param name="Sent">Always true (we never reveal whether the account exists).</param>
/// <param name="ExpiresInSeconds">How long a freshly sent code stays valid.</param>
public sealed record ResendOtpResponse(bool Sent, int ExpiresInSeconds);
