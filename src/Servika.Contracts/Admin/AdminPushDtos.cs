namespace Servika.Contracts.Admin;

/// <summary>The browser's PushSubscription.toJSON() (POST /admin/push/subscriptions).</summary>
public sealed record AdminPushSubscribeRequest(string Endpoint, AdminPushKeys Keys);

public sealed record AdminPushKeys(string P256dh, string Auth);

/// <summary>DELETE /admin/push/subscriptions body.</summary>
public sealed record AdminPushUnsubscribeRequest(string Endpoint);

/// <summary>What the console needs to subscribe: the VAPID public key, or null when push is off.</summary>
public sealed record AdminPushConfigDto(string? PublicKey, int DevicesForYou);
