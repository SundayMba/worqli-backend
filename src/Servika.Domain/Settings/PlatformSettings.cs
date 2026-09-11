namespace Servika.Domain.Settings;

/// <summary>
/// The marketplace's admin-controlled business settings — a single row (there is
/// only ever one). Keeps the commercial levers (commission, payout floor, referral
/// reward, auto-confirm window) out of code so the admin can tune them without a
/// redeploy. Defaults match the launch-window constants they replace.
/// </summary>
public sealed class PlatformSettings
{
    /// <summary>The one and only settings row's id.</summary>
    public static readonly Guid SingletonId = new("22222222-2222-2222-2222-222222222222");

    public Guid Id { get; private set; }

    /// <summary>Commission taken on a standard booking, as a fraction 0–1 (0 = launch window).</summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>Commission taken on an urgent/emergency booking (typically higher).</summary>
    public decimal EmergencyCommissionRate { get; private set; }

    /// <summary>Hours a job may sit AwaitingConfirmation before it auto-confirms.</summary>
    public int AutoConfirmHours { get; private set; }

    /// <summary>Smallest payout (Naira) an artisan or referrer may withdraw.</summary>
    public int MinWithdrawalNaira { get; private set; }

    /// <summary>Reward (Naira) credited to a referrer on a referred artisan's first job.</summary>
    public int ReferralRewardNaira { get; private set; }

    /// <summary>How much unpaid cash-job commission an artisan may carry before
    /// they stop receiving new job requests (the enforcement floor).</summary>
    public int MaxCommissionDebtNaira { get; private set; }

    /// <summary>Whether an artisan must add guarantors before their application can be sent (off by default: recommended, not required). Admin can switch it on, or waive per artisan when on.</summary>
    public bool RequireGuarantors { get; private set; }
    /// <summary>How many guarantors count as complete when they are required.</summary>
    public int RequiredGuarantorCount { get; private set; } = 2;

    // ── Transaction fees (the gateway's charges and who pays them) ──
    /// <summary>When customers and artisans start paying their own transaction fees.
    /// Null = not scheduled: Servika absorbs every fee (the launch window).</summary>
    public DateTimeOffset? FeesStartAtUtc { get; private set; }
    /// <summary>Gateway percentage on a payment, as a fraction (Paystack local: 0.015).</summary>
    public decimal CardFeeRate { get; private set; } = 0.015m;
    /// <summary>Flat gateway amount added on payments of at least <see cref="CardFeeFlatFromNaira"/> (Paystack: ₦100 from ₦2,500).</summary>
    public int CardFeeFlatNaira { get; private set; } = 100;
    public int CardFeeFlatFromNaira { get; private set; } = 2500;
    /// <summary>Cap on the gateway's payment fee (Paystack: ₦2,000). 0 = no cap.</summary>
    public int CardFeeCapNaira { get; private set; } = 2000;
    /// <summary>Transfer-out charge bands (Paystack: ₦10 up to ₦5,000, ₦25 up to ₦50,000, ₦50 above).</summary>
    public int TransferFeeTier1Naira { get; private set; } = 10;
    public int TransferFeeTier1MaxNaira { get; private set; } = 5000;
    public int TransferFeeTier2Naira { get; private set; } = 25;
    public int TransferFeeTier2MaxNaira { get; private set; } = 50000;
    public int TransferFeeTier3Naira { get; private set; } = 50;
    /// <summary>Highest advance-notice stage already sent for the current <see cref="FeesStartAtUtc"/>
    /// (0 none, 1 = 30 days, 2 = 7 days, 3 = 1 day, 4 = live). Resets when the date changes.</summary>
    public int FeeNoticeStage { get; private set; }
    public DateTimeOffset? FeeNoticeSentAtUtc { get; private set; }

    public const int FeeNotice30Days = 1, FeeNotice7Days = 2, FeeNotice1Day = 3, FeeNoticeLive = 4;

    /// <summary>The admin who last changed the settings (audit).</summary>
    public Guid? UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private PlatformSettings() { }

    /// <summary>The default settings a fresh marketplace starts with.</summary>
    public static PlatformSettings Default(DateTimeOffset now) => new()
    {
        Id = SingletonId,
        CommissionRate = 0m,
        EmergencyCommissionRate = 0m,
        AutoConfirmHours = 48,
        MinWithdrawalNaira = 1000,
        ReferralRewardNaira = 500,
        MaxCommissionDebtNaira = 2000,
        RequireGuarantors = false,
        RequiredGuarantorCount = 2,
        UpdatedAtUtc = now,
    };

    /// <summary>Applies an admin update. Values are range-guarded so bad input can't
    /// wedge the marketplace (negative fee, a commission over 100%, etc.).</summary>
    public void Update(
        decimal commissionRate,
        decimal emergencyCommissionRate,
        int autoConfirmHours,
        int minWithdrawalNaira,
        int referralRewardNaira,
        int maxCommissionDebtNaira,
        DateTimeOffset now,
        bool requireGuarantors = false,
        int requiredGuarantorCount = 2)
    {
        CommissionRate = Rate(commissionRate, nameof(commissionRate));
        EmergencyCommissionRate = Rate(emergencyCommissionRate, nameof(emergencyCommissionRate));

        if (autoConfirmHours < 1)
            throw new ArgumentException("Auto-confirm window must be at least 1 hour.", nameof(autoConfirmHours));
        if (minWithdrawalNaira < 0)
            throw new ArgumentException("Minimum withdrawal can't be negative.", nameof(minWithdrawalNaira));
        if (referralRewardNaira < 0)
            throw new ArgumentException("Referral reward can't be negative.", nameof(referralRewardNaira));
        if (maxCommissionDebtNaira < 0)
            throw new ArgumentException("Commission-debt limit can't be negative.", nameof(maxCommissionDebtNaira));

        AutoConfirmHours = autoConfirmHours;
        MinWithdrawalNaira = minWithdrawalNaira;
        ReferralRewardNaira = referralRewardNaira;
        if (requiredGuarantorCount is < 1 or > 5)
            throw new ArgumentException("Required guarantors must be between 1 and 5.", nameof(requiredGuarantorCount));

        MaxCommissionDebtNaira = maxCommissionDebtNaira;
        RequireGuarantors = requireGuarantors;
        RequiredGuarantorCount = requiredGuarantorCount;
        UpdatedAtUtc = now;
    }

    /// <summary>Records who made the change (audit trail for a money lever).</summary>
    public void StampUpdatedBy(Guid? adminUserId) => UpdatedByUserId = adminUserId;

    /// <summary>
    /// Sets the transaction-fee schedule. Rates are range-guarded (a percentage over
    /// 10% or a transfer charge over ₦1,000 is a typo, not a policy). Changing the
    /// start date resets the advance-notice stages so users are warned again for the
    /// new date; clearing it (null) returns to Servika absorbing every fee.
    /// </summary>
    public void UpdateFees(
        DateTimeOffset? feesStartAtUtc,
        decimal cardFeeRate,
        int cardFeeFlatNaira,
        int cardFeeFlatFromNaira,
        int cardFeeCapNaira,
        int transferFeeTier1Naira,
        int transferFeeTier1MaxNaira,
        int transferFeeTier2Naira,
        int transferFeeTier2MaxNaira,
        int transferFeeTier3Naira,
        DateTimeOffset now)
    {
        if (cardFeeRate is < 0m or > 0.10m)
            throw new ArgumentException("The payment fee rate must be between 0% and 10%.", nameof(cardFeeRate));
        if (cardFeeFlatNaira is < 0 or > 5000)
            throw new ArgumentException("The flat payment fee must be between ₦0 and ₦5,000.", nameof(cardFeeFlatNaira));
        if (cardFeeFlatFromNaira < 0)
            throw new ArgumentException("The flat-fee threshold can't be negative.", nameof(cardFeeFlatFromNaira));
        if (cardFeeCapNaira < 0)
            throw new ArgumentException("The payment fee cap can't be negative.", nameof(cardFeeCapNaira));
        foreach (var (v, n) in new[] { (transferFeeTier1Naira, nameof(transferFeeTier1Naira)), (transferFeeTier2Naira, nameof(transferFeeTier2Naira)), (transferFeeTier3Naira, nameof(transferFeeTier3Naira)) })
            if (v is < 0 or > 1000)
                throw new ArgumentException("A transfer charge must be between ₦0 and ₦1,000.", n);
        if (transferFeeTier1MaxNaira <= 0 || transferFeeTier2MaxNaira <= transferFeeTier1MaxNaira)
            throw new ArgumentException("Transfer charge bands must increase: band 1 max below band 2 max.", nameof(transferFeeTier2MaxNaira));

        if (feesStartAtUtc != FeesStartAtUtc)
        {
            FeeNoticeStage = 0;
            FeeNoticeSentAtUtc = null;
        }
        FeesStartAtUtc = feesStartAtUtc;
        CardFeeRate = cardFeeRate;
        CardFeeFlatNaira = cardFeeFlatNaira;
        CardFeeFlatFromNaira = cardFeeFlatFromNaira;
        CardFeeCapNaira = cardFeeCapNaira;
        TransferFeeTier1Naira = transferFeeTier1Naira;
        TransferFeeTier1MaxNaira = transferFeeTier1MaxNaira;
        TransferFeeTier2Naira = transferFeeTier2Naira;
        TransferFeeTier2MaxNaira = transferFeeTier2MaxNaira;
        TransferFeeTier3Naira = transferFeeTier3Naira;
        UpdatedAtUtc = now;
    }

    /// <summary>Records that an advance-notice stage went out (monotonic).</summary>
    public void MarkFeeNoticeSent(int stage, DateTimeOffset now)
    {
        if (stage <= FeeNoticeStage) return;
        FeeNoticeStage = stage;
        FeeNoticeSentAtUtc = now;
    }

    private static decimal Rate(decimal value, string name) =>
        value is < 0m or > 1m
            ? throw new ArgumentException("A commission rate must be a fraction between 0 and 1.", name)
            : value;
}
