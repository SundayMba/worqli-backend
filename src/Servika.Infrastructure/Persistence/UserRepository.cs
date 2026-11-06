using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>. This is the only
/// place that knows how the auth use cases actually reach Postgres. Add* methods
/// just track entities on the DbContext; SaveChangesAsync flushes them in one
/// transaction.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly ServikaDbContext _db;

    public UserRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public async Task<User?> FindByEmailOrPhoneAsync(string emailOrPhone, CancellationToken ct)
    {
        var value = emailOrPhone.Trim();
        if (value.Length == 0) return null;
        // An email is only ever matched as an email, so a phone field can never shadow someone's address.
        if (value.Contains('@'))
            return await _db.Users.FirstOrDefaultAsync(u => u.Email == value.ToLowerInvariant(), ct);
        // Phones are not unique: when two accounts carry the same number, the number identifies nobody.
        var matches = await _db.Users.Where(u => u.PhoneNumber == value).Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken ct) =>
        _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<Guid>> ListSoftDeletedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.DeletedAtUtc != null && u.DeletedAtUtc < cutoff)
            .Select(u => u.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<User>> ListAsync(Role? role, CancellationToken ct)
    {
        // Admins see soft-deleted accounts too (to restore or purge them).
        var query = _db.Users.IgnoreQueryFilters().AsQueryable();
        if (role is { } r)
            query = query.Where(u => u.Role == r);
        return await query.OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
    }

    public Task<User?> FindByReferralCodeAsync(string code, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.ReferralCode == code, ct);

    public Task<RefreshToken?> FindRefreshTokenAsync(string token, CancellationToken ct) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public void AddUser(User user) => _db.Users.Add(user);

    public void RemoveUser(User user) => _db.Users.Remove(user);

    public void AddRefreshToken(RefreshToken token) => _db.RefreshTokens.Add(token);

    public Task<int> RevokeAllRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
