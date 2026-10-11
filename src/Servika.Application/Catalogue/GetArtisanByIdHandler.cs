using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Returns a single artisan's full profile, or 404 if unknown.</summary>
public sealed class GetArtisanByIdHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _services;
    private readonly IBookingRepository _bookings;

    public GetArtisanByIdHandler(
        ICatalogueRepository catalogue, IArtisanServiceRepository services, IBookingRepository bookings)
    {
        _catalogue = catalogue;
        _services = services;
        _bookings = bookings;
    }

    public async Task<ArtisanDetailDto> HandleAsync(Guid id, double? lat, double? lng, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(id, ct)
            ?? throw new NotFoundException($"Artisan '{id}' was not found.");

        var priced = await _services.ListForArtisanAsync(artisan.Id, ct);
        var completed = await _bookings.CountCompletedForArtisansAsync(new[] { artisan.Id }, ct);
        return artisan.ToDetailDto(
            artisan.DistanceFrom(lat, lng),
            completed.GetValueOrDefault(artisan.Id),
            priced.Where(s => s.IsActive).Select(s => s.ToDto()).ToList());
    }
}
