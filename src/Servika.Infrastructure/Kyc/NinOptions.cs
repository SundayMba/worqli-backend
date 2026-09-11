namespace Servika.Infrastructure.Kyc;

/// <summary>
/// NIN register lookup provider. Bound from the `Nin` section. With no ApiKey the dev
/// stub answers offline; with one, the named provider is called (Dojah today).
/// Set via user-secrets locally or `Nin__ApiKey` / `Nin__AppId` on the server.
/// </summary>
public sealed class NinOptions
{
    public const string SectionName = "Nin";

    /// <summary>"dojah" (default). Other providers slot in behind the same port.</summary>
    public string Provider { get; init; } = "dojah";
    public string ApiKey { get; init; } = string.Empty;
    /// <summary>Dojah also needs the app id from its dashboard.</summary>
    public string AppId { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.dojah.io";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
