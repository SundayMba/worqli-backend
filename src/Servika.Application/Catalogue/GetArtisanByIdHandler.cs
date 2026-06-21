using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Returns a single artisan's full profile, or 404 if unknown.</summary>
public sealed class GetArtisanByIdHandler
{
    private readonly ICatalogueRepository _catalogue;

    public GetArtisanByIdHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<ArtisanDetailDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(id, ct)
            ?? throw new NotFoundException($"Artisan '{id}' was not found.");

        return artisan.ToDetailDto();
    }
}
