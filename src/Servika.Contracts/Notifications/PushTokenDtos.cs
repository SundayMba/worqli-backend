namespace Servika.Contracts.Notifications;

/// <summary>Register a device's Expo push token (POST /notifications/push-token).</summary>
public sealed record RegisterPushTokenRequest(string Token, string? Platform);

/// <summary>Remove a device's push token, e.g. on logout (DELETE /notifications/push-token).</summary>
public sealed record RemovePushTokenRequest(string Token);
