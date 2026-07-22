namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Configuration for the phone-OTP provider (bound from the "Sms" config section /
/// env vars like <c>Sms__ApiKey</c>). Defaults target <b>Termii</b> (Nigeria). When
/// <see cref="ApiKey"/> is blank we fall back to the dev stub that logs codes, so
/// local dev needs no secret and spends nothing.
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>Provider API key (secret). Blank locally → the stub sender.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Registered sender id / WhatsApp sender name (Termii "from").</summary>
    public string SenderId { get; init; } = "Servika";

    /// <summary>Provider base URL (Termii NG by default).</summary>
    public string BaseUrl { get; init; } = "https://api.ng.termii.com";

    /// <summary>Try WhatsApp before falling back to SMS (cheaper + more deliverable
    /// in Nigeria). Set false to send SMS only.</summary>
    public bool WhatsAppFirst { get; init; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
