using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
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

    public SaveArtisanServiceHandler(
        ICatalogueRepository catalogue, IArtisanServiceRepository services,
        IFileStorage files, IClock clock)
    {
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
    private readonly IFileStorage _files;

    public GetServicePhotoHandler(IArtisanServiceRepository services, IFileStorage files)
    {
        _services = services;
        _files = files;
    }

    public async Task<StoredFile> HandleAsync(Guid serviceId, CancellationToken ct)
    {
        var service = await _services.FindAsync(serviceId, ct)
            ?? throw new NotFoundException("Service was not found.");
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

        var ranked = all
            .Where(s => s.IsActive && artisans.ContainsKey(s.ArtisanProfileId))
            .Select(s =>
            {
                var a = artisans[s.ArtisanProfileId];
                return (service: s, artisan: a, distance: DistanceKm(lat, lng, a));
            })
            .OrderByDescending(x => x.artisan.IsAvailable)
            .ThenByDescending(x => x.artisan.RankScore)
            .ThenBy(x => x.distance ?? double.MaxValue);

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
            artisan.CategorySlugs.FirstOrDefault(),
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
        var service = await _services.FindAsync(serviceId, ct)
            ?? throw new NotFoundException("Service was not found.");
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
            s.IsActive, s.DurationMinutes, s.Includes, s.Description);
}
