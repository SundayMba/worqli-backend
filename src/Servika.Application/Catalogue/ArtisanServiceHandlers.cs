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
/// The Home discovery rail: fixed-price services across VERIFIED artisans,
/// bookable in one tap. Ranked like the catalogue — available artisans first,
/// then RankScore (rating + certificate), then proximity to the caller's
/// coords when given. Capped so Home stays a rail, not a feed.
/// </summary>
public sealed class GetFeaturedServicesHandler
{
    private readonly IArtisanServiceRepository _services;
    private readonly ICatalogueRepository _catalogue;

    public const int MaxResults = 12;

    public GetFeaturedServicesHandler(
        IArtisanServiceRepository services, ICatalogueRepository catalogue)
    {
        _services = services;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<FeaturedServiceDto>> HandleAsync(
        double? lat, double? lng, CancellationToken ct)
    {
        var all = await _services.ListAllAsync(ct);
        if (all.Count == 0) return Array.Empty<FeaturedServiceDto>();

        // Verified artisans only — the same gate as every public catalogue read.
        var artisans = (await _catalogue.GetArtisansAsync(null, ct)).ToDictionary(a => a.Id);

        return all
            .Where(s => artisans.ContainsKey(s.ArtisanProfileId))
            .Select(s =>
            {
                var a = artisans[s.ArtisanProfileId];
                double? distance =
                    lat is { } la && lng is { } ln && a is { Latitude: { } alat, Longitude: { } alng }
                        ? Math.Round(GeoDistance.Km(la, ln, alat, alng), 1)
                        : null;
                return (service: s, artisan: a, distance);
            })
            .OrderByDescending(x => x.artisan.IsAvailable)
            .ThenByDescending(x => x.artisan.RankScore)
            .ThenBy(x => x.distance ?? double.MaxValue)
            .Take(MaxResults)
            .Select(x => new FeaturedServiceDto(
                x.service.Id, x.service.Name, x.service.PriceNaira,
                string.IsNullOrEmpty(x.service.PhotoKey) ? null : $"/api/v1/services/{x.service.Id}/photo",
                x.artisan.Id, x.artisan.FullName, x.artisan.Rating, x.artisan.ReviewCount,
                x.artisan.HasCertificate,
                string.IsNullOrEmpty(x.artisan.PhotoKey) ? null : $"/api/v1/artisans/{x.artisan.Id}/photo",
                x.artisan.IsAvailable, x.distance))
            .ToList();
    }
}

internal static class ArtisanServiceMapping
{
    public static ArtisanServiceDto ToDto(this ArtisanService s) =>
        new(s.Id, s.Name, s.PriceNaira,
            string.IsNullOrEmpty(s.PhotoKey) ? null : $"/api/v1/services/{s.Id}/photo");
}
