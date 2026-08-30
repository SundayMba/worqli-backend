namespace Servika.Domain.Bookings;

/// <summary>Lifecycle of the (single) materials advance on a booking. Stored as a string.</summary>
public enum MaterialsAdvanceStatus
{
    None = 0,
    /// <summary>The artisan asked; the customer hasn't answered.</summary>
    Requested = 1,
    /// <summary>The customer approved: the amount is in the artisan's wallet now.</summary>
    Approved = 2,
    /// <summary>The customer said no. The artisan may ask again (a smaller amount, or later).</summary>
    Declined = 3,
}
