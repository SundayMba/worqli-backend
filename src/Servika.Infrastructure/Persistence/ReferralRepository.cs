using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Referrals;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IReferralRepository"/>. Shares the
/// scoped DbContext so a reward's wallet credit + status change commit together.</summary>
public sealed class ReferralRepository : IReferralRepository
{
    private readonly ServikaDbContext _db;

    public ReferralRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Referral referral) => _db.Referrals.Add(referral);

    // Tracked so MarkEarned persists on SaveChanges.
    public Task<Referral?> FindByReferredUserAsync(Guid referredUserId, CancellationToken ct) =>
        _db.Referrals.FirstOrDefaultAsync(r => r.ReferredUserId == referredUserId, ct);

    public async Task<IReadOnlyList<Referral>> ListForReferrerAsync(Guid referrerUserId, CancellationToken ct) =>
        await _db.Referrals
            .AsNoTracking()
            .Where(r => r.ReferrerUserId == referrerUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
