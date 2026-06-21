namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Configuration for the Resend email gateway (bound from the "Resend" config
/// section / env vars like <c>Resend__ApiKey</c>). When <see cref="ApiKey"/> is
/// blank we fall back to the dev logging sender, so local dev needs no secret.
/// </summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>Resend API key (secret). Leave blank locally to log codes instead.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Sender address. Defaults to Resend's shared test sender.</summary>
    public string FromEmail { get; init; } = "onboarding@resend.dev";

    /// <summary>Sender display name shown in the inbox.</summary>
    public string FromName { get; init; } = "Servika";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
