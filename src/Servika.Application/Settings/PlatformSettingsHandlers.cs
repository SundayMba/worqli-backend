using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Settings;
using Servika.Domain.Settings;

namespace Servika.Application.Settings;

/// <summary>Maps platform settings to its DTO.</summary>
internal static class PlatformSettingsMapping
{
    public static PlatformSettingsDto ToDto(this PlatformSettings s) =>
        new(s.CommissionRate, s.EmergencyCommissionRate, s.AutoConfirmHours,
            s.MinWithdrawalNaira, s.ReferralRewardNaira, s.UpdatedAtUtc);
}

/// <summary>Reads the current platform settings (admin).</summary>
public sealed class GetPlatformSettingsHandler
{
    private readonly IPlatformSettingsRepository _settings;

    public GetPlatformSettingsHandler(IPlatformSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<PlatformSettingsDto> HandleAsync(CancellationToken ct) =>
        (await _settings.GetOrCreateAsync(ct)).ToDto();
}

/// <summary>Applies an admin update to the platform settings.</summary>
public sealed class UpdatePlatformSettingsHandler
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public UpdatePlatformSettingsHandler(IPlatformSettingsRepository settings, IClock clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public async Task<PlatformSettingsDto> HandleAsync(
        UpdatePlatformSettingsRequest request, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        settings.Update(
            request.CommissionRate,
            request.EmergencyCommissionRate,
            request.AutoConfirmHours,
            request.MinWithdrawalNaira,
            request.ReferralRewardNaira,
            _clock.UtcNow);
        await _settings.SaveChangesAsync(ct);
        return settings.ToDto();
    }
}
