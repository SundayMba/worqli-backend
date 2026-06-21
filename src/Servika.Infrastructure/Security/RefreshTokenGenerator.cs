using System.Security.Cryptography;
using Servika.Application.Abstractions.Security;

namespace Servika.Infrastructure.Security;

/// <summary>
/// Produces cryptographically-random refresh-token secrets. Unlike a JWT, a
/// refresh token carries no data — it's just an unguessable opaque string we
/// look up in the database. Expiry is driven by JwtOptions.RefreshTokenDays.
/// </summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32; // 256 bits of entropy

    private readonly JwtOptions _options;

    public RefreshTokenGenerator(JwtOptions options)
    {
        _options = options;
    }

    public RefreshTokenValue Generate(DateTimeOffset nowUtc)
    {
        // Base64Url -> URL/JSON-safe, no padding characters.
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        var expiresAt = nowUtc.AddDays(_options.RefreshTokenDays);
        return new RefreshTokenValue(token, expiresAt);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
