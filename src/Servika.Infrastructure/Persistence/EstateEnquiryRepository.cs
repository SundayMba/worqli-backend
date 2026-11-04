using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of estate enquiries.</summary>
public sealed class EstateEnquiryRepository : IEstateEnquiryRepository
{
    private readonly ServikaDbContext _db;

    public EstateEnquiryRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(EstateEnquiry enquiry) => _db.EstateEnquiries.Add(enquiry);

    public async Task<IReadOnlyList<EstateEnquiry>> ListAsync(CancellationToken ct) =>
        await _db.EstateEnquiries.AsNoTracking().OrderByDescending(e => e.CreatedAtUtc).Take(500).ToListAsync(ct);

    public Task<EstateEnquiry?> FindForUpdateAsync(Guid id, CancellationToken ct) =>
        _db.EstateEnquiries.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
