using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of the artisan guarantor list.</summary>
public sealed class ArtisanGuarantorRepository : IArtisanGuarantorRepository
{
    private readonly ServikaDbContext _db;

    public ArtisanGuarantorRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ArtisanGuarantor>> ListForUserAsync(Guid artisanUserId, CancellationToken ct) =>
        await _db.ArtisanGuarantors
            .AsNoTracking()
            .Where(g => g.ArtisanUserId == artisanUserId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(ct);

    public Task<int> CountForUserAsync(Guid artisanUserId, CancellationToken ct) =>
        _db.ArtisanGuarantors.CountAsync(g => g.ArtisanUserId == artisanUserId, ct);

    public Task<ArtisanGuarantor?> FindForUserAsync(Guid id, Guid artisanUserId, CancellationToken ct) =>
        _db.ArtisanGuarantors.FirstOrDefaultAsync(g => g.Id == id && g.ArtisanUserId == artisanUserId, ct);

    public void Add(ArtisanGuarantor guarantor) => _db.ArtisanGuarantors.Add(guarantor);
    public void Remove(ArtisanGuarantor guarantor) => _db.ArtisanGuarantors.Remove(guarantor);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
