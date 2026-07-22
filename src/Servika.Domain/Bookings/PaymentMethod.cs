namespace Servika.Domain.Bookings;

/// <summary>
/// How the customer chose to settle the agreed price. Online is the default —
/// escrow-protected in-app payment. Cash is the fallback for customers who
/// can't (or won't) pay online: the artisan collects after the job, with the
/// trade-off that escrow protection doesn't apply. Stored as a string.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Pay in-app; funds held in escrow until the job completes.</summary>
    Online = 1,

    /// <summary>Pay the artisan in cash after the service (no escrow).</summary>
    Cash = 2,
}
