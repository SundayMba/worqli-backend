using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Login;

/// <summary>
/// The "log in" use case: verify credentials, then issue a fresh token pair.
/// </summary>
public sealed class LoginUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public LoginUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refreshTokens,
        IClock clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _clock = clock;
    }

    public async Task<AuthResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailOrPhoneAsync(request.EmailOrPhone?.Trim() ?? string.Empty, ct);

        // Same generic failure whether the user is missing or the password is
        // wrong — never disclose which, to avoid account enumeration.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

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
