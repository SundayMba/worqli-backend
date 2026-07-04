using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IArtisanKycRepository"/>.</summary>
public sealed class ArtisanKycRepository : IArtisanKycRepository
{
    private readonly ServikaDbContext _db;

    public ArtisanKycRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(ArtisanKyc submission) => _db.ArtisanKycSubmissions.Add(submission);

    // Tracked so Approve/Reject/Resubmit persist on SaveChanges.
    public Task<ArtisanKyc?> GetForUserAsync(Guid userId, CancellationToken ct) =>
        _db.ArtisanKycSubmissions.FirstOrDefaultAsync(k => k.UserId == userId, ct);

    public async Task<IReadOnlyList<ArtisanKyc>> ListAsync(
        ArtisanVerificationStatus? status, CancellationToken ct)
    {
        var query = _db.ArtisanKycSubmissions.AsNoTracking().AsQueryable();
        if (status is { } s)
            query = query.Where(k => k.Status == s);
        return await query.OrderByDescending(k => k.SubmittedAtUtc).ToListAsync(ct);
    }

    // Tracked so an admin Approve/Reject persists on SaveChanges.
    public Task<ArtisanKyc?> GetByIdForUpdateAsync(Guid id, CancellationToken ct) =>
        _db.ArtisanKycSubmissions.FirstOrDefaultAsync(k => k.Id == id, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
