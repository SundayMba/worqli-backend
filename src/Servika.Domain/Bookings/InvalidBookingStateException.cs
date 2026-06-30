namespace Servika.Domain.Bookings;

/// <summary>
/// Thrown when a booking is asked to make a transition its current state forbids
/// (e.g. cancelling a job already in progress). It is a Domain exception because
/// the rule it guards is a Domain invariant. The API maps it to 409 Conflict.
/// </summary>
public sealed class InvalidBookingStateException : Exception
{
    public InvalidBookingStateException(string message)
        : base(message)
    {
    }
}
