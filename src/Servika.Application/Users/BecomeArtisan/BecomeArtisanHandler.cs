using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.BecomeArtisan;

/// <summary>
/// "Become a Pro" — upgrades the signed-in account's role to Artisan and issues a
/// fresh token pair (the old access token still says Customer, so the client must
/// swap in the returned session for the artisan endpoints to authorize).
/// Idempotent for an account that's already an artisan.
/// </summary>
public sealed class BecomeArtisanHandler
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public BecomeArtisanHandler(
        IUserRepository users,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refreshTokens,
        IClock clock)
    {
        _users = users;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _clock = clock;
    }

    public async Task<AuthResponse> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidCredentialsException();

        user.PromoteToArtisan();

        var now = _clock.UtcNow;
        var access = _jwt.GenerateAccessToken(user);
        var refresh = _refreshTokens.Generate(now);
        _users.AddRefreshToken(RefreshToken.Issue(user.Id, refresh.Token, refresh.ExpiresAtUtc, now));
        await _users.SaveChangesAsync(ct);

        return new AuthResponse(
            AccessToken: access.Token,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc,
            RefreshToken: refresh.Token,
            User: UserMapping.ToDto(user));
    }
}
