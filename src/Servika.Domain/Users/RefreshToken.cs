namespace Servika.Domain.Users;

/// <summary>
/// A long-lived, single-use credential that lets a client obtain a fresh access
/// token without re-entering a password. Unlike the JWT (which is stateless and
/// can't be cancelled before it expires), a refresh token lives in the database,
/// so it can be revoked and rotated. Each one belongs to exactly one user.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    /// <summary>The user this token authenticates. Foreign key to users.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The opaque random secret handed to the client.</summary>
    public string Token { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Set when the token is revoked (logout) or rotated (refresh). Null while live.</summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>True only if it has not been revoked and has not yet expired.</summary>
    public bool IsActive(DateTimeOffset nowUtc) =>
        RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    // EF Core needs this to rebuild a row from the database.
    private RefreshToken() { }

    private RefreshToken(
        Guid id,
        Guid userId,
        string token,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Creates a new refresh token for a user. The raw secret is generated outside (Infrastructure).</summary>
    public static RefreshToken Issue(
        Guid userId,
        string token,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        return new RefreshToken(Guid.NewGuid(), userId, token, expiresAtUtc, createdAtUtc);
    }

    /// <summary>Marks this token as no longer usable (logout, or replaced during rotation).</summary>
    public void Revoke(DateTimeOffset whenUtc) => RevokedAtUtc = whenUtc;
}
