namespace Servika.Application.Common;

/// <summary>
/// Thrown when a suspended account tries to sign in. The API maps it to 403 Forbidden.
/// </summary>
public sealed class AccountSuspendedException : Exception
{
    public AccountSuspendedException()
        : base("This account has been suspended. Please contact support.")
    {
    }
}
