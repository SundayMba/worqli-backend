namespace Servika.Domain.Notifications;

/// <summary>
/// A device's Expo push token, registered by a signed-in user so the backend can
/// deliver push notifications to it. A user may have several (one per device); a
/// token is globally unique (it moves to whichever user last registered it, e.g.
/// after a shared-device logout/login).
/// </summary>
public sealed class PushToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The Expo push token string, e.g. "ExponentPushToken[xxxx]".</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>"ios" / "android" (informational).</summary>
    public string Platform { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    private PushToken() { }

    public static PushToken Register(Guid userId, string token, string? platform, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A push token is required.", nameof(token));

        return new PushToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token.Trim(),
            Platform = string.IsNullOrWhiteSpace(platform) ? "unknown" : platform.Trim().ToLowerInvariant(),
            CreatedAt = now,
            LastSeenAtUtc = now,
        };
    }

    /// <summary>Re-point an existing token to this user + refresh its last-seen.</summary>
    public void Reassign(Guid userId, string? platform, DateTimeOffset now)
    {
        UserId = userId;
        if (!string.IsNullOrWhiteSpace(platform)) Platform = platform.Trim().ToLowerInvariant();
        LastSeenAtUtc = now;
    }
}
