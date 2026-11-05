using Servika.Domain.Notifications;

namespace Servika.Application.Notifications;

/// <summary>
/// The rules that keep each app to its own notices when one account is signed into
/// both the customer app and Servika Pro.
/// </summary>
public static class NotificationAudienceFilter
{
    /// <summary>Reads the feed's optional <c>?audience=</c> query: "customer" or "artisan"
    /// (any case). Blank = no filter (older builds see everything). Anything else is a 400.</summary>
    public static NotificationAudience? ParseQuery(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience)) return null;
        return audience.Trim().ToLowerInvariant() switch
        {
            "customer" => NotificationAudience.Customer,
            "artisan" => NotificationAudience.Artisan,
            _ => throw new ArgumentException("audience must be 'customer' or 'artisan'."),
        };
    }

    /// <summary>Whether a push meant for <paramref name="audience"/> goes to a token registered by
    /// <paramref name="tokenApp"/>. A token from an older build (null app) still gets everything.</summary>
    public static bool Reaches(NotificationAudience audience, string? tokenApp) =>
        audience == NotificationAudience.Any
        || tokenApp is null
        || (audience == NotificationAudience.Customer && tokenApp == PushToken.CustomerApp)
        || (audience == NotificationAudience.Artisan && tokenApp == PushToken.ProApp);

    /// <summary>The tokens a push for <paramref name="audience"/> should go to.</summary>
    public static IReadOnlyList<string> TokensFor(IEnumerable<PushToken> tokens, NotificationAudience audience) =>
        tokens.Where(t => Reaches(audience, t.App)).Select(t => t.Token).ToList();
}
