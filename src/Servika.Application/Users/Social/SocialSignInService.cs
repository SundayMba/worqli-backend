using System.Security.Cryptography;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Referrals;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Social;

/// <summary>
/// What Google and Apple sign-in share once the provider has vouched for an email: log the matching account
/// in, or create a fresh Customer account on the spot, and issue a session. The provider has already
/// verified the email, so there is no OTP step. A new account gets a random, unusable password: such users
/// sign in with the provider, and forgot-password can set a real password later.
/// </summary>
public sealed class SocialSignInService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public SocialSignInService(
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

    public async Task<AuthResponse> SignInAsync(string email, string? fullName, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await _users.FindByEmailOrPhoneAsync(email, ct);

        if (user is null)
        {
            var unusablePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            user = User.Register(
                fullName: string.IsNullOrWhiteSpace(fullName) ? email.Split('@')[0] : fullName.Trim(),
                email: email,
                phoneNumber: string.Empty, // Neither provider gives one; set later in Profile.
                passwordHash: _passwordHasher.Hash(unusablePassword),
                role: Role.Customer,
                createdAt: now);

            // Same as registration: give them their own share code.
            string ownCode;
            do { ownCode = ReferralCodeGenerator.Generate(user.FullName); }
            while (await _users.FindByReferralCodeAsync(ownCode, ct) is not null);
            user.SetReferralCode(ownCode);

            user.MarkEmailVerified(now); // The provider attests the email.
            _users.AddUser(user);
        }
        else
        {
            if (user.IsSuspended)
                throw new AccountSuspendedException();

            // An existing password account signing in with a provider for the first time: the provider's
            // attestation satisfies verify-to-finish too.
            if (user.EmailVerifiedAtUtc is null)
                user.MarkEmailVerified(now);
        }

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
