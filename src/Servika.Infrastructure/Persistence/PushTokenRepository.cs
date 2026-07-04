using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Notifications;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IPushTokenRepository"/>.</summary>
public sealed class PushTokenRepository : IPushTokenRepository
{
    private readonly ServikaDbContext _db;

    public PushTokenRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(PushToken token) => _db.PushTokens.Add(token);

    public Task<PushToken?> FindByTokenAsync(string token, CancellationToken ct) =>
        _db.PushTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task<IReadOnlyList<PushToken>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        await _db.PushTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(ct);

    public async Task RemoveByTokenAsync(string token, CancellationToken ct)
    {
        var existing = await _db.PushTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
        if (existing is not null) _db.PushTokens.Remove(existing);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
