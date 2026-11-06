using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Password;

/// <summary>
/// Completes a password reset: validate the reset token, set the new password,
/// and consume the token so it can't be reused.
/// </summary>
public sealed class ResetPasswordHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IOtpService _otp;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly IAuthThrottle _throttle;

    public ResetPasswordHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IOtpService otp,
        IPasswordHasher passwordHasher,
        IClock clock,
        IAuthThrottle throttle)
    {
        _throttle = throttle;
        _users = users;
        _codes = codes;
        _otp = otp;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task HandleAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword != request.ConfirmPassword)
            throw new ArgumentException("Passwords do not match.");
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        if (!request.NewPassword.Any(char.IsDigit))
            throw new ArgumentException("Password must contain a number.");

        // A 6-digit code isn't unique by hash, so match it against the latest
        // PasswordReset code for this specific account (same shape as verify-otp).
        var user = await _users.FindByEmailOrPhoneAsync(request.EmailOrPhone ?? string.Empty, ct);
        if (user is null)
            throw new InvalidOtpException();

        var now = _clock.UtcNow;
        var code = await _codes.FindLatestAsync(user.Id, OtpPurpose.PasswordReset, ct);
        if (code is null || !code.IsRedeemable(now))
            throw new InvalidOtpException();

        // Wrong code: count the attempt (eventually locks the code) and fail.
        if (!FixedTimeEquals(_otp.Hash(request.TokenOrOtp ?? string.Empty), code.CodeHash))
        {
            code.RegisterFailedAttempt();
            await _codes.SaveChangesAsync(ct);
            throw new InvalidOtpException();
        }

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        code.Consume(now);
        await _users.SaveChangesAsync(ct);

        // Whoever knew the old password is signed out everywhere, and the owner can sign in again at once.
        await _users.RevokeAllRefreshTokensAsync(user.Id, now, ct);
        _throttle.Reset("login:" + user.Email);
        if (!string.IsNullOrEmpty(user.PhoneNumber)) _throttle.Reset("login:" + user.PhoneNumber.ToLowerInvariant());
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
