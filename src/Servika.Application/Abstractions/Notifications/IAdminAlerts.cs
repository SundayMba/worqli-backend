namespace Servika.Application.Abstractions.Notifications;

/// <summary>What an admin alert is about. Money kinds also go to the ops email.</summary>
public enum AdminAlertKind
{
    NewApplication,
    Resubmission,
    ApplicationOverdue,
    DisputeRaised,
    DisputeResponse,
    RefundFailed,
    PaymentMismatch,
    FloatShort,
    DailyDigest,
    /// <summary>Rotation ran out of eligible artisans for a request.</summary>
    NoArtisanFree,
    /// <summary>A custom quote is waiting for ops to check it before the customer sees it.</summary>
    QuoteToCheck,
    /// <summary>An artisan's service post or gallery photo waits for ops to check it.</summary>
    ServicePostToReview,
    /// <summary>An estate or facility manager asked to talk to Servika.</summary>
    EstateEnquiry,
}

/// <summary>
/// One message for the admin desk. Carries names, trade, area, amounts and a
/// console link only: never ID numbers, account numbers or photos, because the
/// channels (Telegram, email) live outside the console login.
/// </summary>
public sealed record AdminAlert(
    AdminAlertKind Kind,
    string Title,
    IReadOnlyList<string> Lines,
    string? LinkPath = null,
    string LinkLabel = "Open in console");

/// <summary>
/// Out-of-app alerts for admins (Telegram first, email as backup). Fire and
/// forget: <see cref="Send"/> returns at once and never throws, so a slow or
/// failing channel cannot delay or roll back the action that raised it.
/// </summary>
public interface IAdminAlerts
{
    void Send(AdminAlert alert);
}
