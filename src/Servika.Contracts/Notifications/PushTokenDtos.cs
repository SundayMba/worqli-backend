namespace Servika.Contracts.Notifications;

/// <summary>Register a device's Expo push token (POST /notifications/push-token).</summary>
/// <param name="App">"customer" or "pro": which app registered the token, so a Pro notice
/// is pushed only to Pro and a customer notice only to the customer app. Omitted by older builds.</param>
public sealed record RegisterPushTokenRequest(string Token, string? Platform, string? App = null);

/// <summary>Remove a device's push token, e.g. on logout (DELETE /notifications/push-token).</summary>
public sealed record RemovePushTokenRequest(string Token);
