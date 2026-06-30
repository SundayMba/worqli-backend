namespace Servika.Domain.Bookings;

/// <summary>
/// Where a booking stands on payment, surfaced to the customer ("Pay now" vs
/// "Paid"). Distinct from the detailed <c>Payment</c> record — this is the booking's
/// at-a-glance state. Stored as a readable string.
/// </summary>
public enum BookingPaymentState
{
    /// <summary>Nothing initialized yet.</summary>
    Unpaid = 0,

    /// <summary>Payment initialized, awaiting the gateway result.</summary>
    Pending = 1,

    /// <summary>Funds received (held in escrow until completion).</summary>
    Paid = 2,

    Refunded = 3,
}
