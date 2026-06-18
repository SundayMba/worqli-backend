using Servika.Domain.Users;

namespace Servika.Application.Abstractions.Security;

/// <summary>A freshly minted access token and the moment it stops being valid.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Mints signed JWT access tokens for a user. Like <see cref="IPasswordHasher"/>,
/// the Application layer depends only on this contract; Infrastructure owns the
/// actual signing key and JWT mechanics.
/// </summary>
public interface IJwtTokenGenerator
{
    AccessToken GenerateAccessToken(User user);
}
