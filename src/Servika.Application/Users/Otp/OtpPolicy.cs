using Servika.Domain.Users;

namespace Servika.Application.Users.Otp;

/// <summary>Central timing/throttle policy for one-time codes.</summary>
internal static class OtpPolicy
{
    public const int NumericCodeTtlSeconds = 600;   // 10 minutes
    public const int ResetTokenTtlSeconds = 1800;   // 30 minutes
    public const int ResendCooldownSeconds = 60;    // min gap between sends

    public static int TtlSecondsFor(OtpPurpose purpose) => purpose == OtpPurpose.PasswordReset
        ? ResetTokenTtlSeconds
        : NumericCodeTtlSeconds;
}

/// <summary>Maps the wire "otp_type" string to the domain <see cref="OtpPurpose"/>.</summary>
internal static class OtpTypeParser
{
    public static OtpPurpose Parse(string? otpType) => (otpType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "account_verification" or "accountverification" or "verify" or "register" => OtpPurpose.AccountVerification,
        "password_reset" or "passwordreset" or "reset" => OtpPurpose.PasswordReset,
        _ => throw new ArgumentException(
            "otpType must be 'account_verification' or 'password_reset'.", nameof(otpType)),
    };
}
