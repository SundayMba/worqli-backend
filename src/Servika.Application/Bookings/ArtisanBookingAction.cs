namespace Servika.Application.Bookings;

/// <summary>
/// The forward state-machine moves the assigned artisan can make on a job. Each
/// maps to a guarded transition on the <c>Booking</c> entity. Kept as one enum
/// (rather than a handler per verb) because the moves are near-identical — resolve
/// the artisan, find their booking, apply the transition, save.
/// </summary>
public enum ArtisanBookingAction
{
    /// <summary>Pending → Accepted.</summary>
    Accept,

    /// <summary>Pending → Rejected.</summary>
    Reject,

    /// <summary>Accepted → OnMyWay (artisan starts the trip).</summary>
    StartTrip,

    /// <summary>OnMyWay → Arrived.</summary>
    Arrive,

    /// <summary>Arrived → InProgress (work begins).</summary>
    StartWork,
}
