namespace Servika.Domain.Payments;

/// <summary>
/// What a gateway payment is for. Booking escrow is the normal case; a
/// commission settlement is an artisan paying off cash-job service fees, so it
/// has no booking and credits the artisan's ledger instead of splitting escrow.
/// Stored as a string.
/// </summary>
public enum PaymentPurpose
{
    /// <summary>A customer paying a booking's agreed price into escrow.</summary>
    BookingEscrow = 1,

    /// <summary>An artisan settling owed cash-job commission.</summary>
    CommissionSettlement = 2,
}
