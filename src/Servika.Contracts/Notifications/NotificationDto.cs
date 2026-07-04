namespace Servika.Contracts.Notifications;

/// <summary>
/// One item in the in-app notification feed (GET /api/v1/notifications).
/// <see cref="Type"/> is a string ("Booking" / "Payment" / "System") the client
/// maps to an icon; <see cref="BookingId"/> deep-links the tap when present.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    Guid? BookingId,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Unread badge count (GET /api/v1/notifications/unread-count).</summary>
public sealed record UnreadCountDto(int Count);
