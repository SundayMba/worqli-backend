using Servika.Domain.Referrals;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for referrals. Shares the scoped DbContext so a reward's
/// wallet credit + the referral's status change commit together.</summary>
public interface IReferralRepository
{
    void Add(Referral referral);

    /// <summary>The referral for a referred user (one per person), tracked so its
    /// status change persists, or null if they weren't referred.</summary>
    Task<Referral?> FindByReferredUserAsync(Guid referredUserId, CancellationToken ct);

    /// <summary>A referrer's referrals, newest first.</summary>
    Task<IReadOnlyList<Referral>> ListForReferrerAsync(Guid referrerUserId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
