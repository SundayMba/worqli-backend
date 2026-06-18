using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Servika.Application.Abstractions.Security;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Security;

/// <summary>
/// Builds and signs JWT access tokens. A JWT is three base64 parts —
/// header.payload.signature. The payload carries "claims" (facts about the user,
/// e.g. their id and role); the signature is computed with our secret key so the
/// API can later trust the claims without a database lookup.
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(JwtOptions options)
    {
        _options = options;
    }

    public AccessToken GenerateAccessToken(User user)
    {
        // The signing key turns our secret string into the material used to sign
        // the token with HMAC-SHA256. Anyone without this secret cannot forge a
        // valid token, and any tampering breaks the signature.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        // Claims = the facts baked into the token. The API reads these to know
        // "who is calling" and "what role they have" on every request.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            // A unique id per token, so a specific token can be identified later.
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(encoded, expiresAt);
    }
}
