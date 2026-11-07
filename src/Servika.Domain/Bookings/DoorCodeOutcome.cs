namespace Servika.Domain.Bookings;

/// <summary>What happened when the artisan entered the door code.</summary>
public enum DoorCodeOutcome
{
    Confirmed,
    Wrong,
    /// <summary>Too many wrong tries: the customer must show a new code.</summary>
    Locked,
}
