using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The signed-in artisan's own profile. A **404** means they haven't onboarded yet
/// — the Pro app uses that to route to the onboarding flow. Carries the guarantor
/// count so the verification hub can show its progress from one call.
/// </summary>
public sealed class GetMyArtisanProfileHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly IPlatformSettingsRepository _settings;

    public GetMyArtisanProfileHandler(ICatalogueRepository catalogue, IArtisanGuarantorRepository guarantors, IPlatformSettingsRepository settings)
    {
        _catalogue = catalogue;
        _guarantors = guarantors;
        _settings = settings;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("You haven't set up your artisan profile yet.");
        // A verified artisan gets their personal booking code the first time they open the app.
        if (await new BookingCodeAssigner(_catalogue).EnsureAsync(profile, ct))
            await _catalogue.SaveChangesAsync(ct);

        return await new MyProfileDtoBuilder(_guarantors, _settings).BuildAsync(profile, artisanUserId, ct);
    }
}
