using Servika.Domain.Notifications;

namespace Servika.Application.Abstractions.Persistence;

public interface IWebPushSubscriptionRepository
{
    Task<IReadOnlyList<WebPushSubscription>> ListAllAsync(CancellationToken ct);
    Task<WebPushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken ct);
    Task<int> CountForUserAsync(Guid userId, CancellationToken ct);
    void Add(WebPushSubscription subscription);
    Task RemoveByEndpointAsync(string endpoint, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
