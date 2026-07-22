using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of the artisan fixed-price service list.</summary>
public sealed class ArtisanServiceRepository : IArtisanServiceRepository
{
    private readonly ServikaDbContext _db;

    public ArtisanServiceRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ArtisanService>> ListForArtisanAsync(
        Guid artisanProfileId, CancellationToken ct) =>
        await _db.ArtisanServices
            .AsNoTracking()
            .Where(s => s.ArtisanProfileId == artisanProfileId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public Task<ArtisanService?> FindAsync(Guid id, CancellationToken ct) =>
        _db.ArtisanServices.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<ArtisanService?> FindByNameAsync(
        Guid artisanProfileId, string name, CancellationToken ct) =>
        _db.ArtisanServices.FirstOrDefaultAsync(
            s => s.ArtisanProfileId == artisanProfileId && s.Name.ToLower() == name.ToLower(),
            ct);

    public void Add(ArtisanService service) => _db.ArtisanServices.Add(service);
    public void Remove(ArtisanService service) => _db.ArtisanServices.Remove(service);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
