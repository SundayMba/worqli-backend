namespace Servika.Domain.Notifications;

/// <summary>
/// Which app a notification belongs to. One account can be signed into both the
/// customer app and Servika Pro, so each notice says whose side it is on: a Pro
/// notice must not land in the customer app (push or feed) and the other way round.
/// </summary>
public enum NotificationAudience
{
    /// <summary>Either app (admin notices, older rows, anything that is genuinely both).</summary>
    Any = 0,

    /// <summary>The customer app.</summary>
    Customer = 1,

    /// <summary>The artisan app (Servika Pro).</summary>
    Artisan = 2,
}
