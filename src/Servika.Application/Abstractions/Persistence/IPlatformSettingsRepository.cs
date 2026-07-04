using Servika.Domain.Settings;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for the single platform-settings row. Reads are how handlers pick up
/// admin-tuned business values (commission, payout floor, …) at runtime.
/// </summary>
public interface IPlatformSettingsRepository
{
    /// <summary>The current settings, creating the default row on first access.
    /// Tracked, so an admin update persists on SaveChanges.</summary>
    Task<PlatformSettings> GetOrCreateAsync(CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
