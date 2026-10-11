using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Admin;
using Servika.Domain.Notifications;

namespace Servika.Application.Admin;

/// <summary>An admin turns on phone notifications for this device (Web Push).</summary>
public sealed class SubscribeAdminPushHandler
{
    private readonly IWebPushSubscriptionRepository _subs;
    private readonly IClock _clock;

    public SubscribeAdminPushHandler(IWebPushSubscriptionRepository subs, IClock clock)
    {
        _subs = subs;
        _clock = clock;
    }

    public async Task HandleAsync(Guid adminUserId, AdminPushSubscribeRequest request, string? userAgent, CancellationToken ct)
    {
        var existing = await _subs.FindByEndpointAsync(request.Endpoint, ct);
        if (existing is not null) existing.Refresh(adminUserId, request.Keys.P256dh, request.Keys.Auth);
        else _subs.Add(WebPushSubscription.Create(adminUserId, request.Endpoint, request.Keys.P256dh, request.Keys.Auth, userAgent, _clock.UtcNow));
        await _subs.SaveChangesAsync(ct);
    }
}

public sealed class UnsubscribeAdminPushHandler
{
    private readonly IWebPushSubscriptionRepository _subs;
    public UnsubscribeAdminPushHandler(IWebPushSubscriptionRepository subs) => _subs = subs;

    public Task HandleAsync(string endpoint, CancellationToken ct) => _subs.RemoveByEndpointAsync(endpoint, ct);
}
