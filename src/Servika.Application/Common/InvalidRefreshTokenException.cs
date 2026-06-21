namespace Servika.Application.Common;

/// <summary>
/// Thrown when a refresh token is unknown, already revoked, or expired. The API
/// maps this to 401 Unauthorized so the client knows to re-authenticate.
/// </summary>
public sealed class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException()
        : base("The refresh token is invalid or has expired.")
    {
    }
}
