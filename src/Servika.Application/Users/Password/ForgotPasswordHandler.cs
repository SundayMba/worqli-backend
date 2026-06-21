using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Users.Otp;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Password;

/// <summary>
/// Starts a password reset: if the account exists, issue a 6-digit reset code and
/// send it. Always returns success — never reveals whether the account exists.
/// </summary>
public sealed class ForgotPasswordHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IOtpService _otp;
    private readonly IOtpSender _sender;
    private readonly IClock _clock;

    public ForgotPasswordHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IOtpService otp,
        IOtpSender sender,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _otp = otp;
        _sender = sender;
        _clock = clock;
    }

    public async Task<ForgotPasswordResponse> HandleAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailOrPhoneAsync(request.EmailOrPhone ?? string.Empty, ct);
        if (user is not null)
        {
            var now = _clock.UtcNow;
            // 6-digit numeric code (entered in-app), not a long link token.
            var code = _otp.GenerateNumericCode();

            _codes.Add(VerificationCode.Issue(
                user.Id,
                OtpPurpose.PasswordReset,
                _otp.Hash(code),
                now.AddSeconds(OtpPolicy.ResetTokenTtlSeconds),
                now));

            // Send before commit: a delivery failure leaves no dangling code.
            await _sender.SendAsync(user.Email, code, OtpPurpose.PasswordReset, ct);
            await _codes.SaveChangesAsync(ct);
        }

        return new ForgotPasswordResponse(ResetStarted: true);
    }
}
