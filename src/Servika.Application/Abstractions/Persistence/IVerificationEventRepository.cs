using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Append-only history of verification applications.</summary>
public interface IVerificationEventRepository
{
    void Add(VerificationEvent evt);
    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<VerificationEvent>> ListForKycAsync(Guid kycId, CancellationToken ct);
}
