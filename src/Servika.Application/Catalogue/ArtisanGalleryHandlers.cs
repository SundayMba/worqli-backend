using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The artisan's self-managed work gallery — evidence photos of finished jobs,
/// shown on their public profile. The artisan adds after a job and can delete
/// any photo; customers read them via the public gallery endpoint.
/// </summary>
public sealed class AddGalleryPhotoHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public AddGalleryPhotoHandler(ICatalogueRepository catalogue, IFileStorage files)
    {
        _catalogue = catalogue;
        _files = files;
    }

    public async Task<GalleryDto> HandleAsync(
        Guid artisanUserId, AddGalleryPhotoRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your Pro profile first.");

        var bytes = DecodeImage(request.PhotoBase64);
        var key = await _files.SaveAsync(bytes, "image/jpeg", ct);
        profile.AddGalleryPhoto(key);
        await _catalogue.SaveChangesAsync(ct);

        return new GalleryDto(profile.GalleryUrls());
    }

    private static byte[] DecodeImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new ArgumentException("A photo is required.");
        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:") && comma >= 0 ? base64[(comma + 1)..] : base64;
        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0) throw new ArgumentException("The photo is empty.");
            return bytes;
        }
        catch (FormatException)
        {
            throw new ArgumentException("The photo is not valid base64.");
        }
    }
}

/// <summary>Deletes one of the artisan's own gallery photos by key.</summary>
public sealed class RemoveGalleryPhotoHandler
{
    private readonly ICatalogueRepository _catalogue;

    public RemoveGalleryPhotoHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<GalleryDto> HandleAsync(
        Guid artisanUserId, string photoKey, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your Pro profile first.");

        if (!profile.RemoveGalleryPhoto(photoKey))
            throw new NotFoundException("That photo isn't in your gallery.");
        await _catalogue.SaveChangesAsync(ct);

        // The stored file is left in place (cheap, and old URLs just 404 via
        // the ownership check below) — a cleanup sweep can reap orphans later.
        return new GalleryDto(profile.GalleryUrls());
    }
}

/// <summary>
/// Serves one gallery photo publicly. The key must belong to the artisan's
/// gallery — a deleted or foreign key is a 404, so nothing leaks.
/// </summary>
public sealed class GetArtisanGalleryPhotoHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public GetArtisanGalleryPhotoHandler(ICatalogueRepository catalogue, IFileStorage files)
    {
        _catalogue = catalogue;
        _files = files;
    }

    public async Task<StoredFile> HandleAsync(Guid artisanId, string photoKey, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException("Artisan was not found.");
        if (!artisan.GalleryPhotoKeys.Contains(photoKey))
            throw new NotFoundException("No such gallery photo.");

        return await _files.GetAsync(photoKey, ct)
            ?? throw new NotFoundException("The photo file was not found.");
    }
}
