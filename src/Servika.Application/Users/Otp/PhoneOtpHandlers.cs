using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Otp;

/// <summary>
/// Sends a phone-verification code to the signed-in user's phone number (over
/// SMS/WhatsApp). Authenticated — verifies the caller's own phone, never someone
/// else's. Cooldown-throttled; a no-op (AlreadyVerified) if the phone is verified.
/// </summary>
public sealed class SendPhoneOtpHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IOtpService _otp;
    private readonly IPhoneOtpSender _sender;
    private readonly IClock _clock;

    public SendPhoneOtpHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IOtpService otp,
        IPhoneOtpSender sender,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _otp = otp;
        _sender = sender;
        _clock = clock;
    }

    public async Task<SendPhoneOtpResponse> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account was not found.");

        if (user.IsPhoneVerified)
            return new SendPhoneOtpResponse(Sent: false, AlreadyVerified: true, ExpiresInSeconds: 0);

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            throw new ArgumentException("Add a phone number to your profile first.");

        var now = _clock.UtcNow;
        var ttl = OtpPolicy.TtlSecondsFor(OtpPurpose.PhoneVerification);

        var last = await _codes.FindLatestAsync(user.Id, OtpPurpose.PhoneVerification, ct);
        if (last is not null && (now - last.CreatedAtUtc).TotalSeconds < OtpPolicy.ResendCooldownSeconds)
            throw new TooManyRequestsException("Please wait before requesting another code.");

        // Per-user daily cap — each SMS/WhatsApp send costs money; bound the spend
        // (and abuse) per account. The 60s cooldown above stops rapid bursts.
        var sentToday = await _codes.CountSinceAsync(
            user.Id, OtpPurpose.PhoneVerification, now.AddHours(-24), ct);
        if (sentToday >= OtpPolicy.MaxPhoneSendsPerDay)
            throw new TooManyRequestsException(
                "You've requested too many codes today. Please try again tomorrow.");

        var code = _otp.GenerateNumericCode();
        _codes.Add(VerificationCode.Issue(
            user.Id, OtpPurpose.PhoneVerification, _otp.Hash(code), now.AddSeconds(ttl), now));

        // Send before commit so a delivery failure leaves no dangling code.
        await _sender.SendAsync(user.PhoneNumber, code, ct);
        await _codes.SaveChangesAsync(ct);

        return new SendPhoneOtpResponse(Sent: true, AlreadyVerified: false, ExpiresInSeconds: ttl);
    }
}

/// <summary>
/// Verifies the signed-in user's phone-verification code and marks their phone
/// verified. Authenticated; idempotent (already-verified returns success).
/// </summary>
public sealed class VerifyPhoneOtpHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IOtpService _otp;
    private readonly IClock _clock;

    public VerifyPhoneOtpHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IOtpService otp,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _otp = otp;
        _clock = clock;
    }

    public async Task<VerifyPhoneOtpResponse> HandleAsync(
        Guid userId, VerifyPhoneOtpRequest request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account was not found.");

        if (user.IsPhoneVerified)
            return new VerifyPhoneOtpResponse(true, UserMapping.ToDto(user));

        var now = _clock.UtcNow;
        var code = await _codes.FindLatestAsync(user.Id, OtpPurpose.PhoneVerification, ct);
        if (code is null || !code.IsRedeemable(now))
            throw new InvalidOtpException();

        if (!FixedTimeEquals(_otp.Hash(request.OtpCode ?? string.Empty), code.CodeHash))
        {
            code.RegisterFailedAttempt();
            await _codes.SaveChangesAsync(ct);
            throw new InvalidOtpException();
        }

        code.Consume(now);
        user.MarkPhoneVerified(now);
        await _users.SaveChangesAsync(ct);

        return new VerifyPhoneOtpResponse(true, UserMapping.ToDto(user));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++) result |= a[i] ^ b[i];
        return result == 0;
    }
}
