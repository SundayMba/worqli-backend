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

    private static decimal Rate(decimal value, string name) =>
        value is < 0m or > 1m
            ? throw new ArgumentException("A commission rate must be a fraction between 0 and 1.", name)
            : value;
}
