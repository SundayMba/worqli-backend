namespace Servika.Domain.Bookings;

/// <summary>
/// How the price of an open request gets determined (the customer chooses when
/// posting). Stored as a string.
/// </summary>
public enum AssessmentMode
{
    /// <summary>The artisan comes over, inspects the job and discusses the
    /// price. Open requests in this mode are first-come-first-served claims.</summary>
    Inspection = 1,

    /// <summary>The job can be priced from the customer's photos/video — the
    /// request is broadcast for artisans to <b>bid</b> their price, and the
    /// customer picks a bidder.</summary>
    RemoteQuote = 2,
}
