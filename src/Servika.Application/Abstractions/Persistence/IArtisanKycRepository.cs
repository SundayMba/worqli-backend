using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for artisan KYC submissions (one per artisan account).</summary>
public interface IArtisanKycRepository
{
    void Add(ArtisanKyc submission);

    /// <summary>The artisan's KYC submission (tracked, so approve/resubmit persists),
    /// or null if they haven't submitted yet.</summary>
    Task<ArtisanKyc?> GetForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>All KYC submissions for the admin queue, newest first, optionally one
    /// status only.</summary>
    Task<IReadOnlyList<ArtisanKyc>> ListAsync(ArtisanVerificationStatus? status, CancellationToken ct);

    /// <summary>A submission by id (tracked, so an admin decision persists), or null.</summary>
    Task<ArtisanKyc?> GetByIdForUpdateAsync(Guid id, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
