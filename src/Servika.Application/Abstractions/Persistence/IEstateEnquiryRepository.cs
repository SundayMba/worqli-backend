using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Estate and facility enquiries.</summary>
public interface IEstateEnquiryRepository
{
    void Add(EstateEnquiry enquiry);
    Task<IReadOnlyList<EstateEnquiry>> ListAsync(CancellationToken ct);
    Task<EstateEnquiry?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
