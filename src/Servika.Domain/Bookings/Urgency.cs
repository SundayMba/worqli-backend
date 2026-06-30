namespace Servika.Domain.Bookings;

/// <summary>How quickly the customer needs the job done. Stored as a string.</summary>
public enum Urgency
{
    /// <summary>Within 24–48h (default).</summary>
    Standard = 0,

    /// <summary>Within 2–4h (attracts the emergency commission band later).</summary>
    Urgent = 1,
}
