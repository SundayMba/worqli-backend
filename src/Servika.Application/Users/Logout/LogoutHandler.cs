using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Auth;

namespace Servika.Application.Users.Logout;

/// <summary>
/// The "logout" use case: revoke the presented refresh token so it can no longer
/// mint access tokens. Idempotent — an unknown or already-revoked token is not
/// an error, so logout always succeeds from the client's point of view.
/// </summary>
public sealed class LogoutHandler
{
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public LogoutHandler(IUserRepository users, IClock clock)
    {
        _users = users;
        _clock = clock;
    }

    public async Task HandleAsync(LogoutRequest request, CancellationToken ct)
    {
        var existing = await _users.FindRefreshTokenAsync(request.RefreshToken ?? string.Empty, ct);
        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.Revoke(_clock.UtcNow);
            await _users.SaveChangesAsync(ct);
        }
    }
}
