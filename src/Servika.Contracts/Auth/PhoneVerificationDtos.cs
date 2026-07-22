namespace Servika.Contracts.Auth;

/// <summary>Result of requesting a phone-verification code (POST /auth/phone/send).</summary>
public sealed record SendPhoneOtpResponse(
    bool Sent,
    bool AlreadyVerified,
    int ExpiresInSeconds);

/// <summary>Submit the phone-verification code (POST /auth/phone/verify).</summary>
public sealed record VerifyPhoneOtpRequest(string OtpCode);

/// <summary>Result of verifying the phone code — returns the updated user.</summary>
public sealed record VerifyPhoneOtpResponse(bool Success, UserDto User);
