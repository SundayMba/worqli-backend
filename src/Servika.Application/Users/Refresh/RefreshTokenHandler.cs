using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Refresh;

/// <summary>
/// The "refresh" use case with rotation: validate the presented refresh token,
/// revoke it, and issue a brand-new token pair. Rotation means a stolen-and-used
/// refresh token is immediately useless to the legitimate client (and vice
/// versa), which surfaces token theft.
/// </summary>
public sealed class RefreshTokenHandler
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public RefreshTokenHandler(
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

    public async Task<TokenResponse> HandleAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var existing = await _users.FindRefreshTokenAsync(request.RefreshToken ?? string.Empty, ct);
        if (existing is null || !existing.IsActive(now))
            throw new InvalidRefreshTokenException();

        var user = await _users.FindByIdAsync(existing.UserId, ct)
            ?? throw new InvalidRefreshTokenException();

        // Rotate: the presented token is spent, a new pair takes its place.
        existing.Revoke(now);

        var access = _jwt.GenerateAccessToken(user);
        var refresh = _refreshTokens.Generate(now);
        _users.AddRefreshToken(RefreshToken.Issue(user.Id, refresh.Token, refresh.ExpiresAtUtc, now));
        await _users.SaveChangesAsync(ct);

        return new TokenResponse(access.Token, access.ExpiresAtUtc, refresh.Token);
    }
}
