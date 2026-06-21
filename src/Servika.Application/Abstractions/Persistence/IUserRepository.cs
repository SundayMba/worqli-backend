using Servika.Domain.Users;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// The persistence operations the auth use cases need, stated in domain terms.
/// The Application layer depends only on this; Infrastructure implements it with
/// EF Core. Add* methods only stage changes — nothing hits the database until
/// SaveChangesAsync, so a user and their refresh token are saved together.
/// </summary>
public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);

    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);

    Task<User?> FindByEmailOrPhoneAsync(string emailOrPhone, CancellationToken ct);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Finds a refresh token by its raw value, or null if unknown.</summary>
    Task<RefreshToken?> FindRefreshTokenAsync(string token, CancellationToken ct);

    void AddUser(User user);

    void AddRefreshToken(RefreshToken token);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
