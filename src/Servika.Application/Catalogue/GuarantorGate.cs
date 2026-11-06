using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// Guarantors are needed before an artisan takes work: sending a price, offering a visit, or accepting a job.
/// They are not needed to apply, get verified, look around or see open jobs, so an artisan can add them any time;
/// the open-job alerts remind them until they do. Off when the admin switch is off or waived for this artisan.
/// A guarantor the reviewer found does not vouch for them does not count.
/// </summary>
public sealed class GuarantorGate
{
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly IPlatformSettingsRepository _settings;

    public GuarantorGate(IArtisanGuarantorRepository guarantors, IPlatformSettingsRepository settings)
    {
        _guarantors = guarantors;
        _settings = settings;
    }

    public static string Missing(int required, int have) =>
        $"Add {(required == 1 ? "a guarantor" : $"{required} guarantors")} before you send prices or accept jobs. You have {have}.";

    /// <summary>How many more this artisan needs to take work (0 = none).</summary>
    public async Task<int> ShortByAsync(ArtisanProfile profile, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        if (!settings.RequireGuarantors || profile.GuarantorsWaived || profile.UserId is not { } userId) return 0;
        var have = await _guarantors.CountForUserAsync(userId, ct);
        return Math.Max(0, settings.RequiredGuarantorCount - have);
    }

    public async Task EnsureAsync(ArtisanProfile profile, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        if (!settings.RequireGuarantors || profile.GuarantorsWaived || profile.UserId is not { } userId) return;
        var have = await _guarantors.CountForUserAsync(userId, ct);
        if (have < settings.RequiredGuarantorCount)
            throw new ConflictException(Missing(settings.RequiredGuarantorCount, have));
    }
}
