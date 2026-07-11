using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;

namespace Servika.Application.Catalogue;

/// <summary>
/// Serves an artisan's uploaded profile photo (the bytes behind the
/// <c>PhotoUrl</c> the catalogue DTOs advertise). 404 when the artisan is
/// unknown or hasn't uploaded a photo — clients fall back to bundled art.
/// </summary>
public sealed class GetArtisanPhotoHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public GetArtisanPhotoHandler(ICatalogueRepository catalogue, IFileStorage files)
    {
        _catalogue = catalogue;
        _files = files;
    }

    /// <param name="cover">True → the cover (at-work) photo; false → the
    /// profile photo.</param>
    public async Task<StoredFile> HandleAsync(Guid artisanId, bool cover, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException("Artisan was not found.");
        var key = cover ? artisan.CoverPhotoKey : artisan.PhotoKey;
        if (string.IsNullOrEmpty(key))
            throw new NotFoundException("This artisan has no such photo.");

        return await _files.GetAsync(key, ct)
            ?? throw new NotFoundException("The photo file was not found.");
    }
}
