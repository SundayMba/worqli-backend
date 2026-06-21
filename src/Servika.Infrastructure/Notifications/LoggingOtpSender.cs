using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// DEV implementation of <see cref="IOtpSender"/>: writes the code to the logs
/// instead of sending real SMS/email. Lets the OTP/reset flows be tested
/// end-to-end now. Replace with a real gateway (Termii/Twilio/SendGrid) in the
/// Notifications module — no use-case code needs to change.
/// </summary>
public sealed class LoggingOtpSender : IOtpSender
{
    private readonly ILogger<LoggingOtpSender> _logger;

    public LoggingOtpSender(ILogger<LoggingOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string destination, string code, OtpPurpose purpose, CancellationToken ct)
    {
        _logger.LogWarning("[DEV-OTP] purpose={Purpose} to={Destination} code={Code}",
            purpose, destination, code);
        return Task.CompletedTask;
    }
}
