using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Favorites;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IFavoriteRepository"/>.</summary>
public sealed class FavoriteRepository : IFavoriteRepository
{
    private readonly ServikaDbContext _db;

    public FavoriteRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Favorite favorite) => _db.Favorites.Add(favorite);

    public Task<Favorite?> FindAsync(Guid userId, Guid artisanId, CancellationToken ct) =>
        _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.ArtisanId == artisanId, ct);

    public async Task RemoveAsync(Guid userId, Guid artisanId, CancellationToken ct)
    {
        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ArtisanId == artisanId, ct);
        if (existing is not null) _db.Favorites.Remove(existing);
    }

    public async Task<IReadOnlyList<Guid>> ListArtisanIdsAsync(Guid userId, CancellationToken ct) =>
        await _db.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.ArtisanId)
            .ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
