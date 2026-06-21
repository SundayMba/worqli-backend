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

    public ResetPasswordHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IOtpService otp,
        IPasswordHasher passwordHasher,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _otp = otp;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task HandleAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword != request.ConfirmPassword)
            throw new ArgumentException("Passwords do not match.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(request));

        var now = _clock.UtcNow;

        // Reset tokens are self-identifying: look up directly by their hash.
        var code = await _codes.FindByHashAsync(
            _otp.Hash(request.TokenOrOtp ?? string.Empty), OtpPurpose.PasswordReset, ct);
        if (code is null || !code.IsRedeemable(now))
            throw new InvalidOtpException();

        var user = await _users.FindByIdAsync(code.UserId, ct)
            ?? throw new InvalidOtpException();

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        code.Consume(now);
        await _users.SaveChangesAsync(ct);
    }
}
