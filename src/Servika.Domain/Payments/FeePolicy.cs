using Servika.Domain.Settings;

namespace Servika.Domain.Payments;

/// <summary>
/// The transaction-fee rules, computed from the admin settings and nothing else.
/// Paystack charges Servika a percentage (plus a flat amount above a threshold,
/// capped) on every card or transfer-in payment, and a tiered flat charge on every
/// transfer out. During the launch window Servika absorbs both; from
/// <see cref="PlatformSettings.FeesStartAtUtc"/> the customer pays the payment fee
/// on top of the price and the artisan pays the transfer charge out of a withdrawal.
///
/// <para>All amounts are whole Naira. Fees round UP so Servika is never short by
/// the odd kobo; the exact amount the gateway took is recorded separately when the
/// webhook reports it.</para>
/// </summary>
public static class FeePolicy
{
    /// <summary>The gateway's fee on a payment of <paramref name="amountNaira"/>.</summary>
    public static int CardFee(int amountNaira, PlatformSettings s)
    {
        if (amountNaira <= 0) return 0;
        var fee = (decimal)amountNaira * s.CardFeeRate;
        if (amountNaira >= s.CardFeeFlatFromNaira) fee += s.CardFeeFlatNaira;
        var rounded = (int)Math.Ceiling(fee);
        return s.CardFeeCapNaira > 0 ? Math.Min(rounded, s.CardFeeCapNaira) : rounded;
    }

    /// <summary>
    /// What to charge on top of <paramref name="netNaira"/> so that, after the
    /// gateway takes its fee from the whole charge, at least <paramref name="netNaira"/>
    /// is left. Solves the fee-on-fee problem (the gateway's percentage applies to the
    /// fee itself too). Returns the fee to add, never negative.
    /// </summary>
    public static int CustomerServiceFee(int netNaira, PlatformSettings s)
    {
        if (netNaira <= 0 || s.CardFeeRate <= 0m && s.CardFeeFlatNaira <= 0) return 0;
        // Start from the simple fee and step up until the net is covered. The loop
        // converges in one or two steps because the fee is small relative to the price.
        var fee = CardFee(netNaira, s);
        for (var i = 0; i < 5; i++)
        {
            var total = netNaira + fee;
            var gatewayTakes = CardFee(total, s);
            if (total - gatewayTakes >= netNaira) break;
            fee = gatewayTakes;
        }
        return fee;
    }

    /// <summary>The gateway's charge for transferring <paramref name="amountNaira"/> to a bank.</summary>
    public static int TransferFee(int amountNaira, PlatformSettings s)
    {
        if (amountNaira <= 0) return 0;
        if (amountNaira <= s.TransferFeeTier1MaxNaira) return s.TransferFeeTier1Naira;
        if (amountNaira <= s.TransferFeeTier2MaxNaira) return s.TransferFeeTier2Naira;
        return s.TransferFeeTier3Naira;
    }

    /// <summary>True once the launch window has ended and users pay their own fees.</summary>
    public static bool UsersBearFees(PlatformSettings s, DateTimeOffset now) =>
        s.FeesStartAtUtc is { } start && now >= start;
}
