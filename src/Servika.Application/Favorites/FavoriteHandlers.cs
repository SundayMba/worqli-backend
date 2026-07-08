using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Favorites;

namespace Servika.Application.Favorites;

/// <summary>Saves an artisan to the customer's favourites (idempotent).</summary>
public sealed class AddFavoriteHandler
{
    private readonly IFavoriteRepository _favorites;
    private readonly ICatalogueRepository _catalogue;
    private readonly IClock _clock;

    public AddFavoriteHandler(
        IFavoriteRepository favorites, ICatalogueRepository catalogue, IClock clock)
    {
        _favorites = favorites;
        _catalogue = catalogue;
        _clock = clock;
    }

    public async Task HandleAsync(Guid userId, Guid artisanId, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException($"Artisan '{artisanId}' was not found.");

        if (await _favorites.FindAsync(userId, artisan.Id, ct) is null)
        {
            _favorites.Add(Favorite.Create(userId, artisan.Id, _clock.UtcNow));
            await _favorites.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Removes an artisan from favourites (idempotent).</summary>
public sealed class RemoveFavoriteHandler
{
    private readonly IFavoriteRepository _favorites;

    public RemoveFavoriteHandler(IFavoriteRepository favorites)
    {
        _favorites = favorites;
    }

    public async Task HandleAsync(Guid userId, Guid artisanId, CancellationToken ct)
    {
        await _favorites.RemoveAsync(userId, artisanId, ct);
        await _favorites.SaveChangesAsync(ct);
    }
}

/// <summary>The customer's saved artisans as summary cards (skips any that were
/// since removed from the catalogue).</summary>
public sealed class GetFavoritesHandler
{
    private readonly IFavoriteRepository _favorites;
    private readonly ICatalogueRepository _catalogue;

    public GetFavoritesHandler(IFavoriteRepository favorites, ICatalogueRepository catalogue)
    {
        _favorites = favorites;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<ArtisanSummaryDto>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var ids = await _favorites.ListArtisanIdsAsync(userId, ct);
        var list = new List<ArtisanSummaryDto>(ids.Count);
        foreach (var id in ids)
        {
            var artisan = await _catalogue.GetArtisanByIdAsync(id, ct);
            if (artisan is not null) list.Add(artisan.ToSummaryDto());
        }
        return list;
    }
}
