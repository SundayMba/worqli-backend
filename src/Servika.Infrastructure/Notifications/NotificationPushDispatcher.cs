using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Turns an in-app notification into a device push <b>without blocking</b> the
/// transaction that produced it. <see cref="Dispatch"/> returns immediately and does
/// the token lookup + send on a background task in its own DI scope (the request's
/// scoped DbContext is gone by then), so a slow or failing push can never delay or
/// roll back a booking/payment.
/// </summary>
public sealed class NotificationPushDispatcher : INotificationPushDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPushSender _sender;
    private readonly ILogger<NotificationPushDispatcher> _logger;

    public NotificationPushDispatcher(
        IServiceScopeFactory scopeFactory,
        IPushSender sender,
        ILogger<NotificationPushDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _sender = sender;
        _logger = logger;
    }

    public void Dispatch(Guid userId, string title, string body, Guid? bookingId, Guid? conversationId = null)
    {
        _ = Task.Run(async () =>
        {
            // Dispatch is called just before the producing transaction commits;
            // give it a beat so a client that refetches on the real-time event
            // actually sees the new row.
            await Task.Delay(500);

            // Real-time (SignalR) first — a signed-in app updates instantly.
            // Registered by the Api host; absent in other hosts (e.g. the Worker).
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var realtime = scope.ServiceProvider.GetService<INotificationRealtimePublisher>();
                if (realtime is not null)
                    await realtime.PublishAsync(userId, title, body, bookingId, conversationId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Realtime notification publish failed for user {UserId}.", userId);
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tokens = scope.ServiceProvider.GetRequiredService<IPushTokenRepository>();
                var registered = await tokens.ListForUserAsync(userId, CancellationToken.None);
                if (registered.Count == 0) return;

                var data = new Dictionary<string, string>();
                if (bookingId is { } b) data["bookingId"] = b.ToString();
                if (conversationId is { } c) data["conversationId"] = c.ToString();

                await _sender.SendAsync(
                    registered.Select(t => t.Token).ToList(), title, body,
                    data.Count > 0 ? data : null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push dispatch failed for user {UserId}.", userId);
            }
        });
    }
}
