using System.Security.Cryptography;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Referrals;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Google;

/// <summary>
/// Sign in (or sign up) with Google. The app hands us the Google ID token; we
/// verify it with Google, then either log the matching account in or create a
/// fresh Customer account on the spot. Google has already verified ownership of
/// the email, so no OTP step is needed — a session is issued immediately (this
/// is the whole point of social login: one tap, no code to type).
///
/// A Google-created account has no usable password (a random unguessable hash
/// is stored); such users sign in with Google. If they later want a password,
/// the forgot-password flow sets one — it emails their verified address.
/// </summary>
public sealed class GoogleLoginHandler
{
    private readonly IGoogleTokenVerifier _google;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public GoogleLoginHandler(
        IGoogleTokenVerifier google,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refreshTokens,
        IClock clock)
    {
        _google = google;
        _users = users;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _clock = clock;
    }

    public async Task<AuthResponse> HandleAsync(GoogleLoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new InvalidCredentialsException();

        var identity = await _google.VerifyAsync(request.IdToken, ct)
            ?? throw new InvalidCredentialsException();

        var now = _clock.UtcNow;
        var user = await _users.FindByEmailOrPhoneAsync(identity.Email, ct);

        if (user is null)
        {
            // First time — create a Customer account from the Google identity.
            // The password is a random secret nobody knows: the account is
            // Google-sign-in-only until the user sets one via forgot-password.
            var unusablePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            user = User.Register(
                fullName: string.IsNullOrWhiteSpace(identity.FullName)
                    ? identity.Email.Split('@')[0]
                    : identity.FullName,
                email: identity.Email,
                phoneNumber: string.Empty, // Google doesn't provide one; set later in Profile.
                passwordHash: _passwordHasher.Hash(unusablePassword),
                role: Role.Customer,
                createdAt: now);

            // Same as registration: give them their own share code.
            string ownCode;
            do { ownCode = ReferralCodeGenerator.Generate(user.FullName); }
            while (await _users.FindByReferralCodeAsync(ownCode, ct) is not null);
            user.SetReferralCode(ownCode);

            user.MarkEmailVerified(now); // Google attests the email — no OTP needed.
            _users.AddUser(user);
        }
        else
        {
            if (user.IsSuspended)
                throw new AccountSuspendedException();

            // An existing password account signing in with Google for the first
            // time: Google's attestation satisfies verify-to-finish too.
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
