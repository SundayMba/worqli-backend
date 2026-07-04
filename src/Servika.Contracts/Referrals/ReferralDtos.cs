namespace Servika.Contracts.Referrals;

/// <summary>
/// The signed-in user's referral dashboard (GET /api/v1/referrals/me): their share
/// code, earnings, and the artisans they've referred.
/// </summary>
public sealed record ReferralSummaryDto(
    string Code,
    int RewardNaira,
    int AvailableNaira,
    int PaidOutNaira,
    int PendingCount,
    IReadOnlyList<ReferredArtisanDto> Referred);

/// <summary>
/// One referred artisan on the dashboard. <see cref="Status"/> is a readable string
/// ("onboarding" / "active" / "earned" / "paid") the app maps to a chip.
/// </summary>
public sealed record ReferredArtisanDto(
    Guid Id,
    string Name,
    string Trade,
    string Status,
    DateTimeOffset CreatedAtUtc);
