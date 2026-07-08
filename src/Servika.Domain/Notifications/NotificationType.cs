namespace Servika.Domain.Notifications;

/// <summary>
/// Coarse category of a notification — enough for the client to pick an icon and
/// group the feed. The specific wording lives in the notification's title/body.
/// </summary>
public enum NotificationType
{
    /// <summary>A booking changed state (accepted, on the way, completed, …).</summary>
    Booking,

    /// <summary>A payment/wallet event (payment received, payout, …).</summary>
    Payment,

    /// <summary>General account / platform message.</summary>
    System,

    /// <summary>A new chat message from the other party in a conversation.</summary>
    Chat,

    /// <summary>An open (unassigned) job an artisan can claim — broadcast to matching
    /// artisans. Tapping it opens the available-jobs list.</summary>
    OpenJob,
}
