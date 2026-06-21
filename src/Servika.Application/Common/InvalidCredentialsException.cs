namespace Servika.Application.Common;

/// <summary>
/// Thrown when login fails (unknown email/phone or wrong password). The message
/// is intentionally generic so we never reveal whether an account exists. The
/// API maps this to 401 Unauthorized.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email/phone or password.")
    {
    }
}
