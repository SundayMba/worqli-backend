using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Otp;

/// <summary>
/// Verifies an OTP. For account verification it consumes the code, marks the
/// account verified, and logs the user in (returns tokens). For password reset
/// it only confirms validity (the reset itself is done by reset-password).
/// </summary>
public sealed class VerifyOtpHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IOtpService _otp;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IClock _clock;

    public VerifyOtpHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IOtpService otp,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refreshTokens,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _otp = otp;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _clock = clock;
    }

    public async Task<VerifyOtpResponse> HandleAsync(VerifyOtpRequest request, CancellationToken ct)
    {
        var purpose = OtpTypeParser.Parse(request.OtpType);

        var user = await _users.FindByEmailOrPhoneAsync(request.EmailOrPhone ?? string.Empty, ct);
        if (user is null)
            throw new InvalidOtpException();

        var now = _clock.UtcNow;
        var code = await _codes.FindLatestAsync(user.Id, purpose, ct);
        if (code is null || !code.IsRedeemable(now))
            throw new InvalidOtpException();

        // Wrong code: record the attempt (so it eventually locks) and fail.
        if (!FixedTimeEquals(_otp.Hash(request.OtpCode ?? string.Empty), code.CodeHash))
        {
            code.RegisterFailedAttempt();
            await _codes.SaveChangesAsync(ct);
            throw new InvalidOtpException();
        }

        if (purpose == OtpPurpose.AccountVerification)
        {
            code.Consume(now);
            user.MarkEmailVerified(now);

            var access = _jwt.GenerateAccessToken(user);
            var refresh = _refreshTokens.Generate(now);
            _users.AddRefreshToken(RefreshToken.Issue(user.Id, refresh.Token, refresh.ExpiresAtUtc, now));
            await _users.SaveChangesAsync(ct);

            // Include the profile so the app can establish a full session in one
            // round-trip (verification is where the user actually logs in).
            return new VerifyOtpResponse(
                true, access.Token, access.ExpiresAtUtc, refresh.Token, UserMapping.ToDto(user));
        }

        // Password reset: this is a validity check only; reset-password consumes it.
        return new VerifyOtpResponse(true);
    }

    // Length-aware comparison that does not short-circuit on the first byte.
    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++) result |= a[i] ^ b[i];
        return result == 0;
    }
}
