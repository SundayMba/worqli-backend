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

    public void Dispatch(Guid userId, string title, string body, Guid? bookingId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tokens = scope.ServiceProvider.GetRequiredService<IPushTokenRepository>();
                var registered = await tokens.ListForUserAsync(userId, CancellationToken.None);
                if (registered.Count == 0) return;

                var data = bookingId is { } b
                    ? new Dictionary<string, string> { ["bookingId"] = b.ToString() }
                    : null;

                await _sender.SendAsync(
                    registered.Select(t => t.Token).ToList(), title, body, data, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push dispatch failed for user {UserId}.", userId);
            }
        });
    }
}
