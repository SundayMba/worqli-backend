using Servika.Domain.Settings;

namespace Servika.Application.Common;

/// <summary>
/// Fixed product rules the apps also need to know (published on GET /api/v1/fees),
/// kept in one place so the server's enforcement and what the apps show can't drift.
/// </summary>
public static class PlatformRules
{
    /// <summary>Hours an artisan application may wait before it counts as overdue on the review desk.</summary>
    public const int ReviewTargetHours = 24;

    /// <summary>Most guarantors an artisan may list.</summary>
    public const int MaxGuarantors = 5;

    /// <summary>Fewest work photos accepted as proof of completion (the materials receipt is separate).</summary>
    public const int ProofPhotosMin = 2;

    /// <summary>Most work photos accepted as proof of completion.</summary>
    public const int ProofPhotosMax = 4;

    /// <summary>Lowest referral cash-out: one reward, never above the general payout floor, never below ₦100.</summary>
    public static int ReferralMinWithdrawalNaira(PlatformSettings s) =>
        Math.Max(100, Math.Min(s.ReferralRewardNaira, s.MinWithdrawalNaira));
}
