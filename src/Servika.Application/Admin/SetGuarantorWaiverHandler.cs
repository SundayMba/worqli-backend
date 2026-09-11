using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Admin;

namespace Servika.Application.Admin;

/// <summary>Admin turns the guarantor requirement off (or back on) for one artisan.</summary>
public sealed class SetGuarantorWaiverHandler
{
    private readonly ICatalogueRepository _catalogue;

    public SetGuarantorWaiverHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task HandleAsync(Guid artisanId, GuarantorWaiverRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.FindArtisanForUpdateAsync(artisanId, ct)
            ?? throw new NotFoundException($"Artisan '{artisanId}' was not found.");
        profile.SetGuarantorsWaived(request.Waived);
        await _catalogue.SaveChangesAsync(ct);
    }
}
