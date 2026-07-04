namespace Servika.Domain.Disputes;

/// <summary>
/// Thrown when a dispute is asked to make a transition its current state forbids
/// (e.g. resolving one that is already resolved). A Domain exception because the
/// rule it guards is a Domain invariant. The API maps it to 409 Conflict.
/// </summary>
public sealed class InvalidDisputeStateException : Exception
{
    public InvalidDisputeStateException(string message)
        : base(message)
    {
    }
}
