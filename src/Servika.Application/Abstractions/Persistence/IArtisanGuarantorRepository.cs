using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>The people who vouch for an artisan (verification hub, admin review).</summary>
public interface IArtisanGuarantorRepository
{
    Task<IReadOnlyList<ArtisanGuarantor>> ListForUserAsync(Guid artisanUserId, CancellationToken ct);
    Task<int> CountForUserAsync(Guid artisanUserId, CancellationToken ct);
    /// <summary>Tracked fetch scoped to the owner, or null.</summary>
    Task<ArtisanGuarantor?> FindForUserAsync(Guid id, Guid artisanUserId, CancellationToken ct);
    /// <summary>Tracked fetch by id (admin), or null.</summary>
    Task<ArtisanGuarantor?> FindByIdForUpdateAsync(Guid id, CancellationToken ct);
    /// <summary>Every guarantor's owner and phone, for spotting numbers reused across artisans.</summary>
    Task<IReadOnlyList<(Guid ArtisanUserId, string Phone)>> ListAllPhonesAsync(CancellationToken ct);
    void Add(ArtisanGuarantor guarantor);
    void Remove(ArtisanGuarantor guarantor);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
