using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IVerificationCodeRepository"/>.</summary>
public sealed class VerificationCodeRepository : IVerificationCodeRepository
{
    private readonly ServikaDbContext _db;

    public VerificationCodeRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(VerificationCode code) => _db.VerificationCodes.Add(code);

    public Task<VerificationCode?> FindLatestAsync(Guid userId, OtpPurpose purpose, CancellationToken ct) =>
        _db.VerificationCodes
            .Where(c => c.UserId == userId && c.Purpose == purpose)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<VerificationCode?> FindByHashAsync(string codeHash, OtpPurpose purpose, CancellationToken ct) =>
        _db.VerificationCodes
            .Where(c => c.CodeHash == codeHash && c.Purpose == purpose)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountSinceAsync(
        Guid userId, OtpPurpose purpose, DateTimeOffset sinceUtc, CancellationToken ct) =>
        _db.VerificationCodes
            .CountAsync(c => c.UserId == userId && c.Purpose == purpose && c.CreatedAtUtc >= sinceUtc, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
