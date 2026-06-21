namespace Servika.Application.Abstractions.Security;

/// <summary>A freshly generated refresh-token secret and when it expires.</summary>
public sealed record RefreshTokenValue(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Produces the opaque random secret used as a refresh token, and computes its
/// expiry from configured policy. Infrastructure owns the randomness source and
/// the "how many days" setting; the use case just asks for one.
/// </summary>
public interface IRefreshTokenGenerator
{
    RefreshTokenValue Generate(DateTimeOffset nowUtc);
}
