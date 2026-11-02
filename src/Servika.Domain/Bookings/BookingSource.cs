namespace Servika.Domain.Bookings;

/// <summary>How the customer reached this artisan (Business Model v1.4, section 10: the commission
/// tier later depends on it). Recorded at creation, never changed.</summary>
public enum BookingSource
{
    /// <summary>Servika picked the artisan in rotation.</summary>
    Rotation = 0,
    /// <summary>The customer chose the artisan in the app (profile, listing).</summary>
    Direct = 1,
    /// <summary>The customer used the artisan's personal booking code or link.</summary>
    Code = 2,
    /// <summary>The customer rebooked an artisan who had already done a job for them.</summary>
    Rebook = 3,
}
