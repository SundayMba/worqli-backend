namespace Servika.Application.Abstractions.Notifications;

/// <summary>
/// Delivers a one-time code to a phone number. The real implementation
/// (Termii / Twilio) sends over <b>WhatsApp first, falling back to SMS</b> —
/// WhatsApp is near-universal in Nigeria, cheaper and more deliverable, and SMS
/// must use a transactional route to bypass the DND registry. A stub logs the
/// code in dev so the flow is testable without a provider account; the real
/// sender slots in behind this port once <c>Sms</c> config is set (the same
/// pattern as Paystack/Resend).
/// </summary>
public interface IPhoneOtpSender
{
    Task SendAsync(string phoneNumber, string code, CancellationToken ct);
}
