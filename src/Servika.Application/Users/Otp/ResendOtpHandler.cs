using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Otp;

/// <summary>
/// Issues (or re-issues) a one-time code and sends it. Throttled per account to
/// resist abuse. Always reports success so it never reveals whether the account
/// exists; throttling only applies to real accounts.
/// </summary>
public sealed class ResendOtpHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IOtpService _otp;
    private readonly IOtpSender _sender;
    private readonly IClock _clock;

    public ResendOtpHandler(
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

    public async Task<ResendOtpResponse> HandleAsync(ResendOtpRequest request, CancellationToken ct)
    {
        var purpose = OtpTypeParser.Parse(request.OtpType);
        var ttl = OtpPolicy.TtlSecondsFor(purpose);

        var user = await _users.FindByEmailOrPhoneAsync(request.EmailOrPhone ?? string.Empty, ct);
        if (user is not null)
        {
            var now = _clock.UtcNow;

            var last = await _codes.FindLatestAsync(user.Id, purpose, ct);
            if (last is not null && (now - last.CreatedAtUtc).TotalSeconds < OtpPolicy.ResendCooldownSeconds)
                throw new TooManyRequestsException("Please wait before requesting another code.");

            var code = purpose == OtpPurpose.PasswordReset
                ? _otp.GenerateResetToken()
                : _otp.GenerateNumericCode();

            _codes.Add(VerificationCode.Issue(user.Id, purpose, _otp.Hash(code), now.AddSeconds(ttl), now));
            await _codes.SaveChangesAsync(ct);

            await _sender.SendAsync(request.EmailOrPhone!, code, purpose, ct);
        }

        return new ResendOtpResponse(Sent: true, ExpiresInSeconds: ttl);
    }
}
