using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>The signed-in artisan's own fixed-price service list.</summary>
public sealed class GetMyArtisanServicesHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _services;

    public GetMyArtisanServicesHandler(
        ICatalogueRepository catalogue, IArtisanServiceRepository services)
    {
        _catalogue = catalogue;
        _services = services;
    }

    public async Task<IReadOnlyList<ArtisanServiceDto>> HandleAsync(
        Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        var list = await _services.ListForArtisanAsync(profile.Id, ct);
        return list.Select(s => s.ToDto()).ToList();
    }
}

/// <summary>Publishes (or reprices) a fixed-price service on the caller's profile.
/// Matching an existing name (case-insensitive) revises that price — no duplicates.</summary>
public sealed class SaveArtisanServiceHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _services;
    private readonly IFileStorage _files;
    private readonly IClock _clock;
    private readonly Abstractions.Notifications.IAdminAlerts _alerts;

    public SaveArtisanServiceHandler(
        ICatalogueRepository catalogue, IArtisanServiceRepository services,
        IFileStorage files, IClock clock, Abstractions.Notifications.IAdminAlerts alerts)
    {
        _alerts = alerts;
        _catalogue = catalogue;
        _services = services;
        _files = files;
        _clock = clock;
    }

    public async Task<ArtisanServiceDto> HandleAsync(
        Guid artisanUserId, SaveArtisanServiceRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        // Store the showcase photo (if any) BEFORE touching the row, so a bad
        // image is a clean 400 with nothing half-saved — same order as the
        // profile-photo upload.
        string? photoKey = null;
        if (!string.IsNullOrWhiteSpace(request.PhotoBase64))
        {
            byte[] bytes;
            try
            {
                var payload = request.PhotoBase64!;
                var comma = payload.IndexOf(',');
                if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
                    payload = payload[(comma + 1)..];
                bytes = Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                throw new ArgumentException("The service photo isn't a valid image upload.");
            }
            photoKey = await _files.SaveAsync(bytes, "image/jpeg", ct);
        }

        var existing = await _services.FindByNameAsync(profile.Id, request.Name?.Trim() ?? "", ct);
        ArtisanService service;
        if (existing is not null)
        {
            existing.Reprice(request.PriceNaira);
            service = existing;
        }
        else
        {
            service = ArtisanService.Create(
                profile.Id, request.Name ?? "", request.PriceNaira, _clock.UtcNow);
            _services.Add(service);
        }
        if (photoKey is not null) service.SetPhoto(photoKey);
        if (request.DurationMinutes is not null || request.Includes is not null || request.Description is not null)
            service.UpdateDetails(
                request.DurationMinutes ?? service.DurationMinutes,
                request.Includes ?? service.Includes,
                request.Description ?? service.Description);

        // The trade it sits under on Home: one of the artisan's own.
        var slug = string.IsNullOrWhiteSpace(request.CategorySlug)
            ? (string.IsNullOrEmpty(service.CategorySlug) ? profile.CategorySlugs.FirstOrDefault() : service.CategorySlug)
            : request.CategorySlug.Trim().ToLowerInvariant();
        if (slug is null || !profile.CategorySlugs.Contains(slug))
            throw new ArgumentException("Pick one of your own trades for this service.");
        service.SetCategory(slug);

        // Whatever changed, ops check it again before customers see it.
        var now = _clock.UtcNow;
        service.SubmitForReview(now);
        _alerts.Send(new Abstractions.Notifications.AdminAlert(Abstractions.Notifications.AdminAlertKind.ServicePostToReview,
            "A service post to check",
            new[] { $"{profile.FullName}: {service.Name}, ₦{service.PriceNaira:N0}.", "Check the cover photo shows the work, with no phone number or address." },
            "/posts", "Review posts"));

        await _services.SaveChangesAsync(ct);
        return service.ToDto();
    }
}

/// <summary>Pauses or resumes one of the caller's listings (design 48). Paused
/// listings leave the customer surfaces but stay on the artisan's own list.</summary>
public sealed class SetArtisanServiceActiveHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _services;

    public SetArtisanServiceActiveHandler(ICatalogueRepository catalogue, IArtisanServiceRepository services)
    {
        _catalogue = catalogue;
        _services = services;
    }

    public async Task<ArtisanServiceDto> HandleAsync(Guid artisanUserId, Guid serviceId, bool active, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        var service = await _services.FindAsync(serviceId, ct);
        if (service is null || service.ArtisanProfileId != profile.Id)
            throw new NotFoundException("That service was not found on your profile.");
        service.SetActive(active);
        await _services.SaveChangesAsync(ct);
        return service.ToDto();
    }
}

/// <summary>Removes one of the caller's own published services (404 otherwise).</summary>
public sealed class DeleteArtisanServiceHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _services;

    public DeleteArtisanServiceHandler(
        ICatalogueRepository catalogue, IArtisanServiceRepository services)
    {
        _catalogue = catalogue;
        _services = services;
    }

    public async Task HandleAsync(Guid artisanUserId, Guid serviceId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var service = await _services.FindAsync(serviceId, ct);
        if (service is null || service.ArtisanProfileId != profile.Id)
            throw new NotFoundException("That service was not found on your profile.");

        _services.Remove(service);
        await _services.SaveChangesAsync(ct);
    }
}

/// <summary>Serves a service's showcase photo (public — it renders on Home).</summary>
public sealed class GetServicePhotoHandler
{
    private readonly IArtisanServiceRepository _services;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public GetServicePhotoHandler(IArtisanServiceRepository services, ICatalogueRepository catalogue, IFileStorage files)
    {
        _services = services;
        _catalogue = catalogue;
        _files = files;
    }

    /// <summary>
    /// A live post's photo is public. One still waiting for ops, or sent back, is shown only to its artisan and
    /// to ops: a new cover can carry a phone number or a shop sign until someone has looked at it.
    /// </summary>
    public async Task<StoredFile> HandleAsync(Guid serviceId, CancellationToken ct, Guid? viewerUserId = null, bool viewerIsAdmin = false)
    {
        var service = await _services.FindAsync(serviceId, ct)
            ?? throw new NotFoundException("Service was not found.");
        if (!service.IsPublic && !viewerIsAdmin)
        {
            var owner = viewerUserId is { } uid ? await _catalogue.GetArtisanByUserIdAsync(uid, ct) : null;
            if (owner is null || owner.Id != service.ArtisanProfileId)
                throw new NotFoundException("Service was not found.");
        }
        if (string.IsNullOrEmpty(service.PhotoKey))
            throw new NotFoundException("This service has no photo.");
        return await _files.GetAsync(service.PhotoKey, ct)
            ?? throw new NotFoundException("The photo file was not found.");
    }
}

/// <summary>
/// Fixed-price service discovery across VERIFIED artisans, bookable in one tap.
/// Ranked like the catalogue — available artisans first, then RankScore (rating +
/// certificate), then proximity to the caller's coords when given. The Home rail
/// takes the top <see cref="MaxFeatured"/>; the "Services close to you" screen
/// takes the whole ranked list.
/// </summary>
public sealed class GetFeaturedServicesHandler
{
    private readonly IArtisanServiceRepository _services;
    private readonly ICatalogueRepository _catalogue;

    /// <summary>Cap for the Home rail, so Home stays a rail, not a feed.</summary>
    public const int MaxFeatured = 12;

    public GetFeaturedServicesHandler(
        IArtisanServiceRepository services, ICatalogueRepository catalogue)
    {
        _services = services;
        _catalogue = catalogue;
    }

    /// <param name="limit">Max results; null = everything.</param>
    public async Task<IReadOnlyList<FeaturedServiceDto>> HandleAsync(
        double? lat, double? lng, int? limit, CancellationToken ct)
    {
        var all = await _services.ListAllAsync(ct);
        if (all.Count == 0) return Array.Empty<FeaturedServiceDto>();

        // Verified artisans only — the same gate as every public catalogue read.
        var artisans = (await _catalogue.GetArtisansAsync(null, ct)).ToDictionary(a => a.Id);

        // Approved, live posts only. Ranked without ratings (ratings are for ops): available artisans first,
        // then nearest, then newest, so a new artisan's post is seen as soon as it passes the check.
        var ranked = all
            .Where(s => s.IsPublic && artisans.ContainsKey(s.ArtisanProfileId))
            .Select(s =>
            {
                var a = artisans[s.ArtisanProfileId];
                return (service: s, artisan: a, distance: DistanceKm(lat, lng, a));
            })
            .OrderByDescending(x => x.artisan.IsAvailable)
            .ThenBy(x => x.distance ?? double.MaxValue)
            .ThenByDescending(x => x.service.CreatedAt);

        return (limit is { } n ? ranked.Take(n) : ranked)
            .Select(x => ToFeaturedDto(x.service, x.artisan, x.distance))
            .ToList();
    }

    internal static double? DistanceKm(double? lat, double? lng, ArtisanProfile a) =>
        lat is { } la && lng is { } ln && a is { Latitude: { } alat, Longitude: { } alng }
            ? Math.Round(GeoDistance.Km(la, ln, alat, alng), 1)
            : null;

    internal static FeaturedServiceDto ToFeaturedDto(
        ArtisanService service, ArtisanProfile artisan, double? distance) =>
        new(
            service.Id, service.Name, service.PriceNaira,
            string.IsNullOrEmpty(service.PhotoKey) ? null : $"/api/v1/services/{service.Id}/photo",
            artisan.Id, artisan.FullName, artisan.Rating, artisan.ReviewCount,
            artisan.HasCertificate,
            string.IsNullOrEmpty(artisan.PhotoKey) ? null : $"/api/v1/artisans/{artisan.Id}/photo",
            artisan.IsAvailable, distance,
            string.IsNullOrEmpty(service.CategorySlug) ? artisan.CategorySlugs.FirstOrDefault() : service.CategorySlug,
            service.DurationMinutes, service.Includes, service.Description);
}

/// <summary>
/// One fixed-price service with its provider's summary — the service profile
/// page. Public like the rest of the catalogue; 404 when the service is unknown
/// or its artisan is no longer verified/listed.
/// </summary>
public sealed class GetServiceHandler
{
    private readonly IArtisanServiceRepository _services;
    private readonly ICatalogueRepository _catalogue;

    public GetServiceHandler(IArtisanServiceRepository services, ICatalogueRepository catalogue)
    {
        _services = services;
        _catalogue = catalogue;
    }

    public async Task<FeaturedServiceDto> HandleAsync(
        Guid serviceId, double? lat, double? lng, CancellationToken ct)
    {
        var service = await _services.FindAsync(serviceId, ct);
        if (service is not { IsPublic: true }) throw new NotFoundException("Service was not found.");
        var artisan = await _catalogue.GetArtisanByIdAsync(service.ArtisanProfileId, ct);
        if (artisan is null || artisan.VerificationStatus != ArtisanVerificationStatus.Verified)
            throw new NotFoundException("Service was not found.");

        return GetFeaturedServicesHandler.ToFeaturedDto(
            service, artisan, GetFeaturedServicesHandler.DistanceKm(lat, lng, artisan));
    }
}

internal static class ArtisanServiceMapping
{
    public static ArtisanServiceDto ToDto(this ArtisanService s) =>
        new(s.Id, s.Name, s.PriceNaira,
            string.IsNullOrEmpty(s.PhotoKey) ? null : $"/api/v1/services/{s.Id}/photo",
            s.IsActive, s.DurationMinutes, s.Includes, s.Description,
            string.IsNullOrEmpty(s.CategorySlug) ? null : s.CategorySlug, s.Review.ToString(), s.ReviewNote);
}

/// <summary>
/// Home, section by section: one row per trade ("Electrical standard services") with the approved posts under it,
/// in the trades' display order. Only trades with at least one live post appear.
/// </summary>
public sealed class GetServiceSectionsHandler
{
    /// <summary>Posts per row; the rest are a tap away on the trade's screen.</summary>
    public const int PerSection = 12;

    private readonly GetFeaturedServicesHandler _featured;
    private readonly ICatalogueRepository _catalogue;

    public GetServiceSectionsHandler(GetFeaturedServicesHandler featured, ICatalogueRepository catalogue)
    {
        _featured = featured;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<ServiceSectionDto>> HandleAsync(double? lat, double? lng, string? categorySlug, CancellationToken ct)
    {
        var categories = (await _catalogue.GetCategoriesAsync(ct)).OrderBy(c => c.SortOrder).ToList();
        var all = await _featured.HandleAsync(lat, lng, null, ct);
        var slug = string.IsNullOrWhiteSpace(categorySlug) ? null : categorySlug.Trim().ToLowerInvariant();
        return categories
            .Where(c => slug is null || c.Slug == slug)
            .Select(c => new ServiceSectionDto(c.Slug, c.Name, $"{c.Name} standard services",
                all.Where(s => s.CategorySlug == c.Slug).Take(slug is null ? PerSection : int.MaxValue).ToList()))
            .Where(s => s.Services.Count > 0)
            .ToList();
    }
}

/// <summary>Service posts for ops, oldest waiting first (or by status).</summary>
public sealed class ListServicePostsHandler
{
    private readonly IArtisanServiceRepository _services;
    private readonly ICatalogueRepository _catalogue;

    public ListServicePostsHandler(IArtisanServiceRepository services, ICatalogueRepository catalogue)
    {
        _services = services;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<AdminServicePostDto>> HandleAsync(string? status, CancellationToken ct)
    {
        ServicePostReview? wanted = string.IsNullOrWhiteSpace(status) ? null
            : Enum.TryParse<ServicePostReview>(status, true, out var r) ? r
            : throw new ArgumentException("Status must be Pending, Approved or Rejected.");
        var categories = (await _catalogue.GetAllCategoriesAsync(ct)).ToDictionary(c => c.Slug, c => c.Name);
        var artisans = (await _catalogue.ListAllArtisansAsync(ct)).ToDictionary(a => a.Id);
        return (await _services.ListAllAsync(ct))
            .Where(s => wanted is null || s.Review == wanted)
            .OrderBy(s => s.Review != ServicePostReview.Pending)
            .ThenBy(s => s.SubmittedAtUtc ?? s.CreatedAt)
            .Select(s =>
            {
                artisans.TryGetValue(s.ArtisanProfileId, out var a);
                return new AdminServicePostDto(
                    s.Id, s.Name, s.PriceNaira,
                    string.IsNullOrEmpty(s.PhotoKey) ? null : $"/api/v1/services/{s.Id}/photo",
                    s.CategorySlug, categories.GetValueOrDefault(s.CategorySlug, s.CategorySlug),
                    s.ArtisanProfileId, a?.FullName ?? "Artisan",
                    a is { PhotoKey: not null and not "" } ? $"/api/v1/artisans/{a.Id}/photo" : null,
                    s.DurationMinutes, s.Includes, s.Description, s.IsActive, s.Review.ToString(), s.ReviewNote,
                    s.SubmittedAtUtc, s.ReviewedAtUtc);
            })
            .ToList();
    }
}

/// <summary>Ops pass a post (customers see it) or turn it down with a note (the artisan fixes and resubmits).</summary>
public sealed class DecideServicePostHandler
{
    private readonly IArtisanServiceRepository _services;
    private readonly ICatalogueRepository _catalogue;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    public DecideServicePostHandler(IArtisanServiceRepository services, ICatalogueRepository catalogue,
        Notifications.NotificationEmitter notifications, IClock clock)
    {
        _services = services;
        _catalogue = catalogue;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task HandleAsync(Guid adminUserId, Guid serviceId, DecidePostRequest request, CancellationToken ct)
    {
        var service = await _services.FindAsync(serviceId, ct) ?? throw new NotFoundException("That post was not found.");
        var now = _clock.UtcNow;
        var approve = request.Outcome?.Trim().ToLowerInvariant() switch
        {
            "approve" => true,
            "reject" => false,
            _ => throw new ArgumentException("Outcome must be 'approve' or 'reject'."),
        };
        if (approve) service.Approve(adminUserId, now);
        else service.Reject(adminUserId, request.Note ?? string.Empty, now);
        var artisan = await _catalogue.GetArtisanByIdAsync(service.ArtisanProfileId, ct);
        if (artisan?.UserId is { } userId) _notifications.ServicePostReviewed(userId, service);
        await _services.SaveChangesAsync(ct);
    }
}
