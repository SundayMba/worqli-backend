using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Users.Otp;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Login;

/// <summary>
/// The "log in" use case: verify credentials, then either issue a fresh token
/// pair (verified accounts) or, for an account whose email is still unverified,
/// (re)send a verification code and tell the app to run the verify step. This
/// keeps the verify-to-finish rule from registration enforced at login too.
/// </summary>
public sealed class LoginUserHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otp;
    private readonly IOtpSender _sender;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public LoginUserHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IPasswordHasher passwordHasher,
        IOtpService otp,
        IOtpSender sender,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refreshTokens,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _passwordHasher = passwordHasher;
        _otp = otp;
        _sender = sender;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _clock = clock;
    }

    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailOrPhoneAsync(request.EmailOrPhone?.Trim() ?? string.Empty, ct);

        // Same generic failure whether the user is missing or the password is
        // wrong — never disclose which, to avoid account enumeration.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var now = _clock.UtcNow;

        // Unverified email → resume verification instead of granting a session.
        if (user.EmailVerifiedAtUtc is null)
        {
            await ResendVerificationCodeAsync(user, now, ct);
            return new LoginResponse(VerificationRequired: true, Email: user.Email, Session: null);
        }

        var access = _jwt.GenerateAccessToken(user);
        var refresh = _refreshTokens.Generate(now);
        _users.AddRefreshToken(RefreshToken.Issue(user.Id, refresh.Token, refresh.ExpiresAtUtc, now));
        await _users.SaveChangesAsync(ct);

        var session = new AuthResponse(
            AccessToken: access.Token,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc,
            RefreshToken: refresh.Token,
            User: UserMapping.ToDto(user));

        return new LoginResponse(VerificationRequired: false, Email: user.Email, Session: session);
    }

    // Issues a fresh account-verification code (sent before commit, like
    // registration), unless one was sent within the resend cooldown — so
    // repeated login attempts don't spam the inbox or trip the throttle.
    private async Task ResendVerificationCodeAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        var last = await _codes.FindLatestAsync(user.Id, OtpPurpose.AccountVerification, ct);
        if (last is not null && (now - last.CreatedAtUtc).TotalSeconds < OtpPolicy.ResendCooldownSeconds)
            return;

        var ttl = OtpPolicy.TtlSecondsFor(OtpPurpose.AccountVerification);
        var code = _otp.GenerateNumericCode();
        _codes.Add(VerificationCode.Issue(
            user.Id, OtpPurpose.AccountVerification, _otp.Hash(code), now.AddSeconds(ttl), now));
        await _sender.SendAsync(user.Email, code, OtpPurpose.AccountVerification, ct);
        await _codes.SaveChangesAsync(ct);
    }
}
