using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

public sealed class VerificationEventRepository : IVerificationEventRepository
{
    private readonly ServikaDbContext _db;

    public VerificationEventRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(VerificationEvent evt) => _db.VerificationEvents.Add(evt);

    public async Task<IReadOnlyList<VerificationEvent>> ListForKycAsync(Guid kycId, CancellationToken ct) =>
        await _db.VerificationEvents.AsNoTracking()
            .Where(e => e.KycId == kycId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
}
