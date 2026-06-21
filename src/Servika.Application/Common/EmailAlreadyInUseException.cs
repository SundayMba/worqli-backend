namespace Servika.Application.Common;

/// <summary>
/// Thrown when registration is attempted with an email that already has an
/// account. The API layer maps this to a 409 Conflict response.
/// </summary>
public sealed class EmailAlreadyInUseException : Exception
{
    public EmailAlreadyInUseException(string email)
        : base($"An account with email '{email}' already exists.")
    {
    }
}
