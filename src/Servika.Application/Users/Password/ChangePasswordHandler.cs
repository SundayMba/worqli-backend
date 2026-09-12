using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;

namespace Servika.Application.Users.Password;

/// <summary>
/// The signed-in user changes their own password. The current password must verify
/// (401 otherwise, same as a bad login), the new one meets the register rules and
/// differs from the old, and every refresh token the account holds is revoked so
/// other devices drop to the sign-in screen within one access-token lifetime.
/// </summary>
public sealed class ChangePasswordHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public ChangePasswordHandler(IUserRepository users, IPasswordHasher hasher, IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task HandleAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(request));
        if (request.NewPassword.Length > 128)
            throw new ArgumentException("Password is too long.", nameof(request));
        if (!request.NewPassword.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one number.", nameof(request));
        if (request.NewPassword != request.ConfirmPassword)
            throw new ArgumentException("The two passwords do not match.", nameof(request));

        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidCredentialsException();

        if (string.IsNullOrEmpty(request.CurrentPassword) || !_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidCredentialsException();
        if (request.CurrentPassword == request.NewPassword)
            throw new ArgumentException("Choose a password different from the current one.", nameof(request));

        user.ChangePassword(_hasher.Hash(request.NewPassword));
        await _users.RevokeAllRefreshTokensAsync(userId, _clock.UtcNow, ct);
        await _users.SaveChangesAsync(ct);
    }
}
