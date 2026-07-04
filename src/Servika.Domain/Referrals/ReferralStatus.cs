namespace Servika.Domain.Referrals;

/// <summary>Lifecycle of a referral toward its ₦ reward.</summary>
public enum ReferralStatus
{
    /// <summary>Referred artisan signed up; reward not yet earned.</summary>
    Pending = 0,

    /// <summary>Referred artisan completed a first job → ₦ credited to the referrer.</summary>
    Earned = 1,

    /// <summary>The referrer withdrew the reward.</summary>
    Paid = 2,
}
