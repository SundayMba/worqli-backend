using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Servika.Application.Abstractions.Security;

namespace Servika.Infrastructure.Security;

/// <summary>
/// Sign in with Apple configuration, bound from the "Apple" config section. <see cref="BundleIds"/> lists the
/// iOS app ids whose tokens we accept (the token's audience), comma-separated: the customer app and Servika Pro.
/// Nothing configured ⇒ Apple sign-in is off and the endpoint rejects every token.
/// </summary>
public sealed class AppleSignInOptions
{
    public const string SectionName = "Apple";

    public string BundleIds { get; init; } = string.Empty;

    public IReadOnlyList<string> Audiences =>
        BundleIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsConfigured => Audiences.Count > 0;
}

/// <summary>
/// Verifies Sign in with Apple identity tokens locally: the signature against Apple's published keys
/// (fetched from appleid.apple.com and cached), the issuer, expiry, and that the audience is one of OUR app
/// ids. Apple's keys rotate, so an unknown key id triggers one refresh before the token is refused.
/// </summary>
public sealed class AppleTokenVerifier : IAppleTokenVerifier
{
    private const string Issuer = "https://appleid.apple.com";
    private const string KeysUrl = "https://appleid.apple.com/auth/keys";
    private static readonly TimeSpan KeysLifetime = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory _httpFactory;
    private readonly AppleSignInOptions _options;
    private readonly ILogger<AppleTokenVerifier> _logger;
    private readonly SemaphoreSlim _keysLock = new(1, 1);
    private IList<SecurityKey> _keys = [];
    private DateTimeOffset _keysFetchedAt = DateTimeOffset.MinValue;

    public AppleTokenVerifier(IHttpClientFactory httpFactory, AppleSignInOptions options, ILogger<AppleTokenVerifier> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<AppleUserInfo?> VerifyAsync(string identityToken, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Apple sign-in attempted but Apple:BundleIds is not configured.");
            return null;
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        if (!handler.CanReadToken(identityToken)) return null;
        var kid = handler.ReadJwtToken(identityToken).Header.Kid;

        var keys = await GetKeysAsync(forceRefresh: false, ct);
        if (kid is not null && keys.All(k => k.KeyId != kid))
            keys = await GetKeysAsync(forceRefresh: true, ct);

        try
        {
            var principal = handler.ValidateToken(identityToken, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudiences = _options.Audiences,
                IssuerSigningKeys = keys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            }, out _);

            var sub = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            // Apple sends email_verified as "true" or true; relay addresses are always verified.
            var verified = principal.FindFirst("email_verified")?.Value;
            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email)) return null;
            if (verified is not null && !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase)) return null;

            return new AppleUserInfo(sub, email.Trim().ToLowerInvariant());
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogInformation("Apple identity token rejected: {Reason}", ex.Message);
            return null;
        }
    }

    private async Task<IList<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _keys.Count > 0 && DateTimeOffset.UtcNow - _keysFetchedAt < KeysLifetime) return _keys;
        await _keysLock.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _keys.Count > 0 && DateTimeOffset.UtcNow - _keysFetchedAt < KeysLifetime) return _keys;
            var json = await _httpFactory.CreateClient("apple-keys").GetStringAsync(KeysUrl, ct);
            _keys = new JsonWebKeySet(json).GetSigningKeys();
            _keysFetchedAt = DateTimeOffset.UtcNow;
            return _keys;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not fetch Apple's sign-in keys.");
            return _keys;
        }
        finally
        {
            _keysLock.Release();
        }
    }
}
