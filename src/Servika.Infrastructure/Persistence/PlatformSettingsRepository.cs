using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Domain.Settings;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core store for the singleton platform-settings row. The row is created with
/// defaults the first time it's read, so there's nothing to seed and no migration
/// data step.
/// </summary>
public sealed class PlatformSettingsRepository : IPlatformSettingsRepository
{
    private readonly ServikaDbContext _db;
    private readonly IClock _clock;

    public PlatformSettingsRepository(ServikaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PlatformSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var settings = await _db.PlatformSettings
            .FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);

        if (settings is null)
        {
            settings = PlatformSettings.Default(_clock.UtcNow);
            _db.PlatformSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }

        return settings;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
