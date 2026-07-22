using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Dev/test stand-in for a real phone-OTP provider (Termii / Twilio Verify).
/// Logs the code instead of sending it, so the phone-verification flow is fully
/// testable without a provider account or spending on SMS. Selected by DI when no
/// <c>Sms</c> provider is configured — the same fallback pattern as the payment /
/// payout / email senders. The real sender (WhatsApp-first, SMS fallback over a
/// transactional route) implements <see cref="IPhoneOtpSender"/> and swaps in when
/// the provider keys are set, with no use-case changes.
/// </summary>
public sealed class StubPhoneOtpSender : IPhoneOtpSender
{
    private readonly ILogger<StubPhoneOtpSender> _logger;

    public StubPhoneOtpSender(ILogger<StubPhoneOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string code, CancellationToken ct)
    {
        _logger.LogInformation("[DEV-SMS-OTP] phone verification code for {Phone}: {Code}", phoneNumber, code);
        return Task.CompletedTask;
    }
}
