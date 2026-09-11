namespace Servika.Contracts.Payments;

/// <summary>
/// The public fee schedule (GET /api/v1/fees): when users start paying their own
/// transaction fees and what those fees are. The apps show this as "Servika covers
/// it until …" / "Payment fee ₦X". Never contains secrets.
/// </summary>
public sealed record FeeScheduleDto(
    /// <summary>When customers and artisans start bearing fees; null = not scheduled yet.</summary>
    DateTimeOffset? FeesStartAtUtc,
    /// <summary>True when fees are live now.</summary>
    bool UsersBearFees,
    /// <summary>Whole days until fees start; null when not scheduled or already live.</summary>
    int? DaysUntilFees,
    decimal CardFeeRate,
    int CardFeeFlatNaira,
    int CardFeeFlatFromNaira,
    int CardFeeCapNaira,
    IReadOnlyList<TransferFeeTierDto> TransferFeeTiers);

/// <summary>One transfer-charge band: amounts up to <see cref="UpToNaira"/> (null = anything above) cost <see cref="FeeNaira"/>.</summary>
public sealed record TransferFeeTierDto(int? UpToNaira, int FeeNaira);

/// <summary>
/// The fees for a concrete amount (GET /api/v1/fees/quote?amount=), computed
/// server-side so both apps show exactly what will be charged.
/// </summary>
public sealed record FeeQuoteDto(
    int AmountNaira,
    /// <summary>Added on top when a customer pays this amount online (0 while Servika covers it).</summary>
    int ServiceFeeNaira,
    /// <summary>Amount + service fee: what the card is charged.</summary>
    int TotalNaira,
    /// <summary>Taken out when an artisan withdraws this amount (0 while Servika covers it).</summary>
    int TransferFeeNaira,
    /// <summary>Amount − transfer fee: what reaches the bank.</summary>
    int NetNaira,
    bool UsersBearFees,
    DateTimeOffset? FeesStartAtUtc);
