using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The artisan's online/offline toggle. Offline artisans sort last in the
/// catalogue (shown as unavailable) and are skipped by open-job broadcasts —
/// "new requests are paused" — while direct bookings remain possible.
/// </summary>
public sealed class SetArtisanAvailabilityHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly MyProfileDtoBuilder _dto;

    public SetArtisanAvailabilityHandler(ICatalogueRepository catalogue, MyProfileDtoBuilder dto)
    {
        _catalogue = catalogue;
        _dto = dto;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(
        Guid artisanUserId, bool available, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your Pro profile first.");

        profile.SetAvailability(available);
        await _catalogue.SaveChangesAsync(ct);
        return await _dto.BuildAsync(profile, artisanUserId, ct);
    }
}
