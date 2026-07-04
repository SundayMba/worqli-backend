namespace Servika.Domain.Referrals;

/// <summary>
/// A referral: one referrer brought one artisan onto Servika. The reward
/// (<see cref="RewardNaira"/>) is credited to the referrer only once the referred
/// artisan completes their first job (<see cref="MarkEarned"/>), and marked
/// <see cref="ReferralStatus.Paid"/> once the referrer withdraws it. One referral
/// per referred user (a person can only be referred once).
/// </summary>
public sealed class Referral
{
    public Guid Id { get; private set; }

    /// <summary>The user who referred (earns the reward).</summary>
    public Guid ReferrerUserId { get; private set; }

    /// <summary>The referred user (the artisan-to-be).</summary>
    public Guid ReferredUserId { get; private set; }

    public ReferralStatus Status { get; private set; }

    public int RewardNaira { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EarnedAtUtc { get; private set; }

    private Referral() { }

    public static Referral Create(
        Guid referrerUserId, Guid referredUserId, int rewardNaira, DateTimeOffset now)
    {
        if (referrerUserId == Guid.Empty)
            throw new ArgumentException("Referrer is required.", nameof(referrerUserId));
        if (referredUserId == Guid.Empty)
            throw new ArgumentException("Referred user is required.", nameof(referredUserId));
        if (referrerUserId == referredUserId)
            throw new ArgumentException("A user cannot refer themselves.");

        return new Referral
        {
            Id = Guid.NewGuid(),
            ReferrerUserId = referrerUserId,
            ReferredUserId = referredUserId,
            Status = ReferralStatus.Pending,
            RewardNaira = rewardNaira,
            CreatedAt = now,
        };
    }

    /// <summary>The referred artisan completed a first job — reward is now owed.
    /// Returns false (no-op) if it was already earned/paid, so it fires once.</summary>
    public bool MarkEarned(DateTimeOffset now)
    {
        if (Status != ReferralStatus.Pending) return false;
        Status = ReferralStatus.Earned;
        EarnedAtUtc = now;
        return true;
    }
}
