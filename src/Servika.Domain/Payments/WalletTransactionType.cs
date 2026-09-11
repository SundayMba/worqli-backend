namespace Servika.Domain.Payments;

/// <summary>
/// The kind of money movement an append-only wallet ledger entry records
/// (PRD §Payments and Wallet). Stored as a readable string. Payout types exist
/// for the later artisan-withdrawal slice; the payments slice writes the first
/// three (+ Refund/Adjustment as needed).
/// </summary>
public enum WalletTransactionType
{
    /// <summary>The customer's payment for a booking (escrow inflow).</summary>
    BookingPayment = 0,

    /// <summary>Servika's commission cut of a booking payment.</summary>
    PlatformCommission = 1,

    /// <summary>Amount owed to the artisan (payment minus commission).</summary>
    ArtisanEarning = 2,

    Refund = 3,
    Adjustment = 4,
    PayoutRequest = 5,
    PayoutCompleted = 6,

    /// <summary>₦ credited to a referrer when their referred artisan completes a first job.</summary>
    ReferralBonus = 7,

    /// <summary>Servika's commission on a CASH job, debited from the artisan's
    /// balance at completion (no money flowed through the platform to deduct
    /// from). Auto-nets against future online earnings; settled explicitly via
    /// <see cref="CommissionSettlement"/> when the artisan pays it off.</summary>
    CommissionDue = 8,

    /// <summary>The artisan paid off owed cash-job commission through the gateway.</summary>
    CommissionSettlement = 9,

    /// <summary>Part of a paid booking's MATERIALS money released to the artisan
    /// before completion, on the customer's explicit approval, so they can buy the
    /// parts. Comes out of the same escrow: the earning released at completion is
    /// reduced by exactly this amount.</summary>
    MaterialsAdvance = 10,

    /// <summary>The payment fee a CUSTOMER paid on top of the price at checkout
    /// (customer −fee, platform +fee). Only written once users bear fees.</summary>
    ServiceFee = 11,

    /// <summary>The transfer charge taken out of an artisan's or referrer's
    /// withdrawal (platform +fee). Only written once users bear fees.</summary>
    TransferFee = 12,

    /// <summary>What the gateway actually charged Servika for a payment or a
    /// transfer (platform −cost). Written on every gateway movement, whoever bore
    /// the fee, so the admin sees the true cost of running money through Paystack.</summary>
    GatewayCost = 13,
}
