using Servika.Domain.Favorites;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for a customer's saved artisans.</summary>
public interface IFavoriteRepository
{
    void Add(Favorite favorite);

    Task<Favorite?> FindAsync(Guid userId, Guid artisanId, CancellationToken ct);

    Task RemoveAsync(Guid userId, Guid artisanId, CancellationToken ct);

    /// <summary>The artisan-profile ids this user has saved, newest first.</summary>
    Task<IReadOnlyList<Guid>> ListArtisanIdsAsync(Guid userId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
