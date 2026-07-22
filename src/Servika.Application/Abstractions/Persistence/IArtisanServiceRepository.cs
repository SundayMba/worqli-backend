using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>An artisan's published fixed-price services.</summary>
public interface IArtisanServiceRepository
{
    Task<IReadOnlyList<ArtisanService>> ListForArtisanAsync(Guid artisanProfileId, CancellationToken ct);

    /// <summary>Tracked fetch by id, or null.</summary>
    Task<ArtisanService?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked case-insensitive name match within one artisan's list —
    /// re-adding a name revises its price instead of duplicating.</summary>
    Task<ArtisanService?> FindByNameAsync(Guid artisanProfileId, string name, CancellationToken ct);

    void Add(ArtisanService service);
    void Remove(ArtisanService service);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
