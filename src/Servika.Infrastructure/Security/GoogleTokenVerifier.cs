using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Security;

namespace Servika.Infrastructure.Security;

/// <summary>
/// Google OAuth configuration, bound from the "Google" config section (alongside
/// the Directions key). Comma-separated because one deploy may accept tokens for
/// several of our OAuth clients (Android + iOS + web all verify against the same
/// backend). No client ids configured ⇒ Google sign-in is off and the endpoint
/// rejects every token.
/// </summary>
public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>Comma-separated OAuth 2.0 client ids allowed as token audience
    /// (typically the WEB client id — the audience of native sign-in ID tokens).</summary>
    public string OAuthClientIds { get; init; } = string.Empty;

    public IReadOnlyList<string> ClientIds =>
        OAuthClientIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsConfigured => ClientIds.Count > 0;
}

/// <summary>
/// Verifies Google ID tokens against Google's tokeninfo endpoint. Google checks
/// the signature and expiry; we additionally check the audience is one of OUR
/// client ids and that Google has verified the email. Kept behind the
/// <see cref="IGoogleTokenVerifier"/> port so a local-JWKS implementation can
/// replace the HTTP call later without touching the use case.
/// </summary>
public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<GoogleTokenVerifier> _logger;

    public GoogleTokenVerifier(
        IHttpClientFactory httpFactory,
        GoogleOAuthOptions options,
        ILogger<GoogleTokenVerifier> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    private sealed record TokenInfo(
        string? aud, string? email, string? email_verified, string? name);

    public async Task<GoogleUserInfo?> VerifyAsync(string idToken, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Google sign-in attempted but Google:OAuthClientIds is not configured.");
            return null;
        }

        try
        {
            var client = _httpFactory.CreateClient("google-tokeninfo");
            using var response = await client.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}", ct);

            // Google returns 400 for anything invalid/expired.
            if (!response.IsSuccessStatusCode) return null;

            var info = await response.Content.ReadFromJsonAsync<TokenInfo>(ct);
            if (info?.email is null) return null;

            // The token must have been minted for one of OUR OAuth clients.
            if (info.aud is null || !_options.ClientIds.Contains(info.aud))
            {
                _logger.LogWarning("Google ID token rejected: audience {Aud} is not ours.", info.aud);
                return null;
            }

            if (!string.Equals(info.email_verified, "true", StringComparison.OrdinalIgnoreCase))
                return null;

            return new GoogleUserInfo(info.email, info.name ?? string.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Google token verification failed.");
            return null;
        }
    }
}
