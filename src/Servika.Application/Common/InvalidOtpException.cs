namespace Servika.Application.Common;

/// <summary>
/// Thrown when an OTP / reset token is wrong, expired, already used, or locked.
/// The API maps this to 400 Bad Request.
/// </summary>
public sealed class InvalidOtpException : Exception
{
    public InvalidOtpException()
        : base("The code is invalid or has expired.")
    {
    }
}
