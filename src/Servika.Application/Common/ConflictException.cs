namespace Servika.Application.Common;

/// <summary>
/// Thrown when a request conflicts with current state (e.g. paying for a booking
/// that is already paid). The API maps this to 409 Conflict.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
