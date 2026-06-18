namespace Servika.Infrastructure.Security;

/// <summary>
/// Strongly-typed view of the "Jwt" section in appsettings (and overridable by
/// environment variables in production, so the real signing key is never
/// committed). Bound once at startup in DependencyInjection.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}
