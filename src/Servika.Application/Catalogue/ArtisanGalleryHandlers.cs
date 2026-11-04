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
    private readonly Abstractions.Notifications.IAdminAlerts _alerts;

    public AddGalleryPhotoHandler(ICatalogueRepository catalogue, IFileStorage files, Abstractions.Notifications.IAdminAlerts alerts)
    {
        _catalogue = catalogue;
        _files = files;
        _alerts = alerts;
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
        _alerts.Send(new Abstractions.Notifications.AdminAlert(Abstractions.Notifications.AdminAlertKind.ServicePostToReview,
            "A recent-work photo to check", new[] { $"{profile.FullName} added a photo to their recent work." }, "/posts", "Review posts"));

        return new GalleryDto(profile.GalleryUrls(), profile.PendingGalleryUrls());
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
        return new GalleryDto(profile.GalleryUrls(), profile.PendingGalleryUrls());
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

    /// <param name="viewerUserId">The caller, when signed in: the owner and admins may also see photos waiting for ops.</param>
    public async Task<StoredFile> HandleAsync(Guid artisanId, string photoKey, CancellationToken ct, Guid? viewerUserId = null, bool viewerIsAdmin = false)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException("Artisan was not found.");
        var waitingAndAllowed = artisan.PendingGalleryPhotoKeys.Contains(photoKey)
            && (viewerIsAdmin || (viewerUserId is { } v && artisan.UserId == v));
        if (!artisan.GalleryPhotoKeys.Contains(photoKey) && !waitingAndAllowed)
            throw new NotFoundException("No such gallery photo.");

        return await _files.GetAsync(photoKey, ct)
            ?? throw new NotFoundException("The photo file was not found.");
    }
}

/// <summary>Recent-work photos waiting for ops, with the image inline (the console has no token for img tags).</summary>
public sealed class ListPendingGalleryPhotosHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public ListPendingGalleryPhotosHandler(ICatalogueRepository catalogue, IFileStorage files)
    {
        _catalogue = catalogue;
        _files = files;
    }

    public async Task<IReadOnlyList<Contracts.Admin.AdminGalleryPhotoDto>> HandleAsync(CancellationToken ct)
    {
        var result = new List<Contracts.Admin.AdminGalleryPhotoDto>();
        foreach (var a in (await _catalogue.ListAllArtisansAsync(ct)).Where(a => a.PendingGalleryPhotoKeys.Count > 0))
        foreach (var key in a.PendingGalleryPhotoKeys)
        {
            var file = await _files.GetAsync(key, ct);
            if (file is null) continue;
            result.Add(new Contracts.Admin.AdminGalleryPhotoDto(a.Id, a.FullName, key,
                $"data:{file.ContentType};base64,{Convert.ToBase64String(file.Content)}"));
        }
        return result;
    }
}

/// <summary>Ops pass a recent-work photo (it shows on the profile) or turn it down (it is deleted, the artisan told why).</summary>
public sealed class DecideGalleryPhotoHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;
    private readonly Notifications.NotificationEmitter _notifications;

    public DecideGalleryPhotoHandler(ICatalogueRepository catalogue, IFileStorage files, Notifications.NotificationEmitter notifications)
    {
        _catalogue = catalogue;
        _files = files;
        _notifications = notifications;
    }

    public async Task HandleAsync(Guid artisanId, string photoKey, Contracts.Admin.DecidePostRequest request, CancellationToken ct)
    {
        var artisan = await _catalogue.FindArtisanForUpdateAsync(artisanId, ct) ?? throw new NotFoundException("Artisan was not found.");
        var approve = request.Outcome?.Trim().ToLowerInvariant() switch
        {
            "approve" => true,
            "reject" => false,
            _ => throw new ArgumentException("Outcome must be 'approve' or 'reject'."),
        };
        var note = request.Note?.Trim();
        if (!approve && (note is null || note.Length < 10))
            throw new ArgumentException("Tell the artisan why (at least a short sentence).");
        var done = approve ? artisan.ApproveGalleryPhoto(photoKey) : artisan.RejectGalleryPhoto(photoKey);
        if (!done) throw new NotFoundException("That photo is not waiting for a check.");
        if (artisan.UserId is { } userId) _notifications.GalleryPhotoReviewed(userId, approve, note);
        await _catalogue.SaveChangesAsync(ct);
        if (!approve) await _files.DeleteAsync(photoKey, ct);
    }
}
