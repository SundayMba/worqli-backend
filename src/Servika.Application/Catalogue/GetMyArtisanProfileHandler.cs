using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The signed-in artisan's own profile. A **404** means they haven't onboarded yet
/// — the Pro app uses that to route to the onboarding flow.
/// </summary>
public sealed class GetMyArtisanProfileHandler
{
    private readonly ICatalogueRepository _catalogue;

    public GetMyArtisanProfileHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("You haven't set up your artisan profile yet.");

        return profile.ToMyProfileDto();
    }
}
