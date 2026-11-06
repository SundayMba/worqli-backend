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
        // Only an account still waiting on a code gets one: a verified account never needs a fresh
        // verification code, and saying nothing different keeps the answer the same for everyone.
        if (user is not null && (purpose == OtpPurpose.PasswordReset || user.EmailVerifiedAtUtc is null))
            await QuietCodeIssuer.IssueAsync(user, purpose, _clock.UtcNow, _codes, _otp, _sender, ct);

        return new ResendOtpResponse(Sent: true, ExpiresInSeconds: ttl);
    }
}
