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

    /// <summary>Which app registered the token: "customer" or "pro". Null for builds from
    /// before the apps said, which keep receiving every push.</summary>
    public string? App { get; private set; }

    public const string CustomerApp = "customer";
    public const string ProApp = "pro";

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    private PushToken() { }

    public static PushToken Register(Guid userId, string token, string? platform, DateTimeOffset now, string? app = null)
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
            App = NormalizeApp(app),
            CreatedAt = now,
            LastSeenAtUtc = now,
        };
    }

    /// <summary>Re-point an existing token to this user + refresh its last-seen.</summary>
    public void Reassign(Guid userId, string? platform, DateTimeOffset now, string? app = null)
    {
        UserId = userId;
        App = NormalizeApp(app);
        if (!string.IsNullOrWhiteSpace(platform)) Platform = platform.Trim().ToLowerInvariant();
        LastSeenAtUtc = now;
    }

    /// <summary>"customer" or "pro" (any case, trimmed); anything else is null.</summary>
    public static string? NormalizeApp(string? app)
    {
        var v = app?.Trim().ToLowerInvariant();
        return v is CustomerApp or ProApp ? v : null;
    }
}
