using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Settings;
using Servika.Domain.Payments;
using Servika.Domain.Settings;

namespace Servika.Application.Settings;

/// <summary>Maps platform settings to its DTO.</summary>
internal static class PlatformSettingsMapping
{
    public static PlatformSettingsDto ToDto(this PlatformSettings s, DateTimeOffset now, string? updatedByName) =>
        new(s.CommissionRate, s.EmergencyCommissionRate, s.AutoConfirmHours,
            s.MinWithdrawalNaira, s.ReferralRewardNaira, s.MaxCommissionDebtNaira, s.UpdatedAtUtc,
            s.RequireGuarantors, s.RequiredGuarantorCount,
            s.FeesStartAtUtc, s.CardFeeRate, s.CardFeeFlatNaira, s.CardFeeFlatFromNaira, s.CardFeeCapNaira,
            s.TransferFeeTier1Naira, s.TransferFeeTier1MaxNaira, s.TransferFeeTier2Naira, s.TransferFeeTier2MaxNaira, s.TransferFeeTier3Naira,
            s.FeeNoticeStage, s.FeeNoticeSentAtUtc, FeePolicy.UsersBearFees(s, now), updatedByName);
}

/// <summary>Reads the current platform settings (admin).</summary>
public sealed class GetPlatformSettingsHandler
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public GetPlatformSettingsHandler(IPlatformSettingsRepository settings, IUserRepository users, IClock clock)
    {
        _settings = settings;
        _users = users;
        _clock = clock;
    }

    public async Task<PlatformSettingsDto> HandleAsync(CancellationToken ct)
    {
        var s = await _settings.GetOrCreateAsync(ct);
        var by = s.UpdatedByUserId is { } id ? (await _users.FindByIdAsync(id, ct))?.FullName : null;
        return s.ToDto(_clock.UtcNow, by);
    }
}

/// <summary>Applies an admin update to the platform settings and records who did it.</summary>
public sealed class UpdatePlatformSettingsHandler
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public UpdatePlatformSettingsHandler(IPlatformSettingsRepository settings, IUserRepository users, IClock clock)
    {
        _settings = settings;
        _users = users;
        _clock = clock;
    }

    public async Task<PlatformSettingsDto> HandleAsync(
        Guid adminUserId, UpdatePlatformSettingsRequest request, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        var now = _clock.UtcNow;
        settings.Update(
            request.CommissionRate,
            request.EmergencyCommissionRate,
            request.AutoConfirmHours,
            request.MinWithdrawalNaira,
            request.ReferralRewardNaira,
            request.MaxCommissionDebtNaira,
            now,
            request.RequireGuarantors,
            request.RequiredGuarantorCount);
        if (request.Fees is { } f)
            settings.UpdateFees(
                f.FeesStartAtUtc, f.CardFeeRate, f.CardFeeFlatNaira, f.CardFeeFlatFromNaira, f.CardFeeCapNaira,
                f.TransferFeeTier1Naira, f.TransferFeeTier1MaxNaira, f.TransferFeeTier2Naira, f.TransferFeeTier2MaxNaira, f.TransferFeeTier3Naira,
                now);
        settings.StampUpdatedBy(adminUserId);
        await _settings.SaveChangesAsync(ct);
        var by = (await _users.FindByIdAsync(adminUserId, ct))?.FullName;
        return settings.ToDto(now, by);
    }
}
