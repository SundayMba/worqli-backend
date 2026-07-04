namespace Servika.Domain.Bookings;

/// <summary>
/// The booking state machine — the spine the whole marketplace hangs off.
/// Stored as a readable string in the database.
///
/// Flow (role-gated transitions enforced in <see cref="Booking"/>):
/// <code>
/// Draft → Pending → Accepted/Rejected → OnMyWay → Arrived → InProgress
///       → Completed | Cancelled | Disputed | Expired
/// </code>
/// Only the customer creates/cancels and confirms Completed; only the assigned
/// artisan advances OnMyWay→Arrived→InProgress. For the booking-happy-path slice
/// the customer side is implemented; the artisan transitions land in a later slice
/// (which is why this enum is modelled in full now — no redesign later).
/// </summary>
public enum BookingStatus
{
    /// <summary>Client-side only: the in-app booking draft before submission.</summary>
    Draft = 0,

    /// <summary>Submitted and awaiting an artisan's response.</summary>
    Pending = 1,

    Accepted = 2,
    Rejected = 3,
    OnMyWay = 4,
    Arrived = 5,
    InProgress = 6,
    Completed = 7,
    Cancelled = 8,
    Disputed = 9,

    /// <summary>Auto-expired (no artisan accepted within the window).</summary>
    Expired = 10,

    /// <summary>Artisan submitted proof of completed work; awaiting the customer's
    /// confirmation (or auto-confirm after the window). InProgress → here → Completed.</summary>
    AwaitingConfirmation = 11,
}
