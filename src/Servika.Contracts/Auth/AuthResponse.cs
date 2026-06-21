namespace Servika.Contracts.Auth;

/// <summary>
/// What register/login return: the short-lived access token (+ its expiry), the
/// long-lived refresh token, and the public user profile. The mobile app stores
/// the two tokens in SecureStore and shows the user as signed in.
/// </summary>
/// <param name="AccessToken">Short-lived signed JWT. Send as "Authorization: Bearer {token}".</param>
/// <param name="AccessTokenExpiresAtUtc">When the access token expires (UTC).</param>
/// <param name="RefreshToken">Long-lived, revocable secret used to obtain new access tokens.</param>
/// <param name="User">The safe public profile (never includes the password hash).</param>
/// <param name="VerificationRequired">True if the account still needs OTP verification before full access.</param>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    UserDto User,
    bool VerificationRequired = false);
