namespace Servika.Infrastructure.Directions;

/// <summary>
/// Google Maps configuration, bound from the "Google" config section. The
/// Directions API key lives in user-secrets / env (never appsettings), exactly
/// like the Paystack/Resend keys. When no key is set, DI falls back to the
/// straight-line stub so local dev and tests run without credentials.
/// </summary>
public sealed class GoogleDirectionsOptions
{
    public const string SectionName = "Google";

    /// <summary>Server-side Directions API key (NOT the client Android Maps key).</summary>
    public string DirectionsApiKey { get; init; } = string.Empty;

    public string DirectionsBaseUrl { get; init; } = "https://maps.googleapis.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(DirectionsApiKey);
}
