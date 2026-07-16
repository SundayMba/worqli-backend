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

    /// <summary>All users for the admin directory, newest first, optionally one role
    /// only. Tracked (an admin suspend/reactivate persists on SaveChanges).</summary>
    Task<IReadOnlyList<User>> ListAsync(Role? role, CancellationToken ct);

    /// <summary>Finds the owner of a referral share code, or null if unknown.</summary>
    Task<User?> FindByReferralCodeAsync(string code, CancellationToken ct);

    /// <summary>Finds a refresh token by its raw value, or null if unknown.</summary>
    Task<RefreshToken?> FindRefreshTokenAsync(string token, CancellationToken ct);

    void AddUser(User user);

    /// <summary>Deletes the account; related rows cascade in the database.</summary>
    void RemoveUser(User user);

    void AddRefreshToken(RefreshToken token);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
