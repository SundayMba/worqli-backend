using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Domain.Users;

namespace Servika.Application.Users.Otp;

/// <summary>
/// Issues an email code for an endpoint that must answer the same whether or not the account exists (forgot
/// password, resend). Too soon after the last code, or past the daily cap, it quietly does nothing instead of
/// returning an error, because an error only for real accounts would tell a stranger who has one. The email goes
/// out after the response, so a real account does not answer more slowly than an unknown address.
/// </summary>
public static class QuietCodeIssuer
{
    /// <summary>Codes of one purpose per account per day; bounds code guessing and inbox flooding.</summary>
    public const int MaxCodesPerDay = 6;

    public static async Task IssueAsync(
        User user, OtpPurpose purpose, DateTimeOffset now,
        IVerificationCodeRepository codes, IOtpService otp, IOtpSender sender, CancellationToken ct)
    {
        var last = await codes.FindLatestAsync(user.Id, purpose, ct);
        if (last is not null && (now - last.CreatedAtUtc).TotalSeconds < OtpPolicy.ResendCooldownSeconds) return;
        if (await codes.CountSinceAsync(user.Id, purpose, now.AddDays(-1), ct) >= MaxCodesPerDay) return;

        var code = otp.GenerateNumericCode();
        codes.Add(VerificationCode.Issue(user.Id, purpose, otp.Hash(code), now.AddSeconds(OtpPolicy.TtlSecondsFor(purpose)), now));
        await codes.SaveChangesAsync(ct);

        var email = user.Email;
        _ = Task.Run(async () =>
        {
            try { await sender.SendAsync(email, code, purpose, CancellationToken.None); }
            catch { /* The sender logs its own failures; the person can ask again after the cooldown. */ }
        });
    }
}
