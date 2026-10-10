using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The one way to build the artisan's own profile DTO. Every endpoint that returns
/// it (read, save, availability, payout, preferences, away) goes through here, so
/// the guarantor count and policy are always real. The app writes these responses
/// straight into its profile cache, so a partial DTO shows wrong state on Home.
/// </summary>
public sealed class MyProfileDtoBuilder
{
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly IPlatformSettingsRepository _settings;

    public MyProfileDtoBuilder(IArtisanGuarantorRepository guarantors, IPlatformSettingsRepository settings)
    {
        _guarantors = guarantors;
        _settings = settings;
    }

    public async Task<MyArtisanProfileDto> BuildAsync(ArtisanProfile profile, Guid artisanUserId, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        return profile.ToMyProfileDto(
            await _guarantors.CountForUserAsync(artisanUserId, ct),
            settings.RequireGuarantors && !profile.GuarantorsWaived,
            settings.RequiredGuarantorCount);
    }
}
