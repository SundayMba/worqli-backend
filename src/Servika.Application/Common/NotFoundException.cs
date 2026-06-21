namespace Servika.Application.Common;

/// <summary>
/// Thrown when a requested resource does not exist (e.g. an unknown artisan id or
/// category slug). The API maps this to 404 Not Found.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
