namespace Servika.Infrastructure.Payments;

/// <summary>
/// Paystack configuration, bound from the "Paystack" config section. The secret
/// key lives in user-secrets / env (never appsettings), exactly like the Resend
/// key. When no key is set, DI falls back to the stub gateway so local dev and
/// tests run without real credentials.
/// </summary>
public sealed class PaystackOptions
{
    public const string SectionName = "Paystack";

    public string SecretKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.paystack.co";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
