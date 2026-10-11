namespace Servika.Domain.Notifications;

/// <summary>
/// A browser/installed-app push subscription for an admin (Web Push). The endpoint
/// is the push service URL the browser handed out; P256dh and Auth are the keys the
/// payload is encrypted with. One row per device/browser.
/// </summary>
public sealed class WebPushSubscription
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private WebPushSubscription() { }

    public static WebPushSubscription Create(Guid userId, string endpoint, string p256dh, string auth, string? userAgent, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(endpoint) || !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A valid push endpoint is required.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
            throw new ArgumentException("The subscription keys are missing.");
        return new WebPushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = endpoint.Trim(),
            P256dh = p256dh.Trim(),
            Auth = auth.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim()[..Math.Min(userAgent.Trim().Length, 200)],
            CreatedAtUtc = now,
        };
    }

    /// <summary>The same device re-subscribed (keys rotate); keep one row per endpoint.</summary>
    public void Refresh(Guid userId, string p256dh, string auth)
    {
        UserId = userId;
        P256dh = p256dh.Trim();
        Auth = auth.Trim();
    }
}
