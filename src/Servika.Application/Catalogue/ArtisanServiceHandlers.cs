using Servika.Application.Abstractions.Persistence;
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
        return list.Select(s => new ArtisanServiceDto(s.Id, s.Name, s.PriceNaira)).ToList();
    }
}

/// <summary>Publishes (or reprices) a fixed-price service on the caller's profile.
/// Matching an existing name (case-insensitive) revises that price — no duplicates.</summary>
public sealed class SaveArtisanServiceHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _services;
    private readonly IClock _clock;

    public SaveArtisanServiceHandler(
        ICatalogueRepository catalogue, IArtisanServiceRepository services, IClock clock)
    {
        _catalogue = catalogue;
        _services = services;
        _clock = clock;
    }

    public async Task<ArtisanServiceDto> HandleAsync(
        Guid artisanUserId, SaveArtisanServiceRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

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

        await _services.SaveChangesAsync(ct);
        return new ArtisanServiceDto(service.Id, service.Name, service.PriceNaira);
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
