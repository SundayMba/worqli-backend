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
    IReadOnlyList<TransferFeeTierDto> TransferFeeTiers,
    // ── Live marketplace settings and fixed product rules the apps show ──
    /// <summary>Commission on a standard job, fraction 0–1 (artisan side).</summary>
    decimal CommissionRate,
    /// <summary>Commission on an urgent job, fraction 0–1.</summary>
    decimal EmergencyCommissionRate,
    /// <summary>Hours a submitted job waits for the customer before it confirms itself.</summary>
    int AutoConfirmHours,
    /// <summary>Smallest artisan withdrawal (Naira).</summary>
    int MinWithdrawalNaira,
    /// <summary>Reward per referred artisan's first job (Naira).</summary>
    int ReferralRewardNaira,
    /// <summary>Smallest referral cash-out (Naira).</summary>
    int ReferralMinWithdrawalNaira,
    /// <summary>Hours an application may wait for a reviewer before it is overdue.</summary>
    int ReviewTargetHours,
    bool RequireGuarantors,
    int RequiredGuarantorCount,
    /// <summary>Most guarantors an artisan may list.</summary>
    int MaxGuarantors,
    /// <summary>Most counter-offers a customer may make on one quote.</summary>
    int MaxCounterRounds,
    /// <summary>Fewest / most work photos accepted as proof of completion.</summary>
    int ProofPhotosMin,
    int ProofPhotosMax,
    /// <summary>Whether a customer may choose cash after service. False = every job is paid in the app.</summary>
    bool AllowCashPayments = false);

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
