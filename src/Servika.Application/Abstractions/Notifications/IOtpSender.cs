using Servika.Domain.Users;

namespace Servika.Application.Abstractions.Notifications;

/// <summary>
/// Delivers a one-time code to the user (SMS/email). The Application layer just
/// asks for delivery; Infrastructure decides how. For now a dev implementation
/// logs the code to the console — swap in a real gateway with the Notifications
/// module without touching the use cases.
/// </summary>
public interface IOtpSender
{
    Task SendAsync(string destination, string code, OtpPurpose purpose, CancellationToken ct);
}
