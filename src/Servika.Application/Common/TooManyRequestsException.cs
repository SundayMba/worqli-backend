namespace Servika.Application.Common;

/// <summary>
/// Thrown when a rate limit is hit (e.g. requesting OTPs too quickly). The API
/// maps this to 429 Too Many Requests.
/// </summary>
public sealed class TooManyRequestsException : Exception
{
    public TooManyRequestsException(string message) : base(message)
    {
    }
}
