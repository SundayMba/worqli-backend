namespace Servika.Domain.Users;

/// <summary>Why a verification code was issued. Drives what verifying it does.</summary>
public enum OtpPurpose
{
    /// <summary>Confirm ownership of email/phone after registration.</summary>
    AccountVerification = 0,

    /// <summary>Authorise a password reset (paired with reset-password).</summary>
    PasswordReset = 1,
}
