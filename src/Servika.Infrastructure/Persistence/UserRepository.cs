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

    public Task<User?> FindByEmailOrPhoneAsync(string emailOrPhone, CancellationToken ct)
    {
        var normalizedEmail = emailOrPhone.Trim().ToLowerInvariant();
        var phone = emailOrPhone.Trim();
        return _db.Users.FirstOrDefaultAsync(
            u => u.Email == normalizedEmail || u.PhoneNumber == phone, ct);
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<RefreshToken?> FindRefreshTokenAsync(string token, CancellationToken ct) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public void AddUser(User user) => _db.Users.Add(user);

    public void AddRefreshToken(RefreshToken token) => _db.RefreshTokens.Add(token);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
