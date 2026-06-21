using Servika.Domain.Users;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for one-time verification codes. Shares the same per-request
/// DbContext as IUserRepository, so a single SaveChangesAsync on either commits
/// both (e.g. consume a code AND change a password in one transaction).
/// </summary>
public interface IVerificationCodeRepository
{
    void Add(VerificationCode code);

    /// <summary>Most recently issued code for a user+purpose (for verify/resend).</summary>
    Task<VerificationCode?> FindLatestAsync(Guid userId, OtpPurpose purpose, CancellationToken ct);

    /// <summary>Find a code by its stored hash, regardless of user (for self-identifying reset tokens).</summary>
    Task<VerificationCode?> FindByHashAsync(string codeHash, OtpPurpose purpose, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
