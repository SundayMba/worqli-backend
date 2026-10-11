using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Notifications;

namespace Servika.Infrastructure.Persistence;

public sealed class WebPushSubscriptionRepository : IWebPushSubscriptionRepository
{
    private readonly ServikaDbContext _db;
    public WebPushSubscriptionRepository(ServikaDbContext db) => _db = db;

    public async Task<IReadOnlyList<WebPushSubscription>> ListAllAsync(CancellationToken ct) =>
        await _db.WebPushSubscriptions.AsNoTracking().ToListAsync(ct);

    public Task<WebPushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken ct) =>
        _db.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

    public Task<int> CountForUserAsync(Guid userId, CancellationToken ct) =>
        _db.WebPushSubscriptions.CountAsync(s => s.UserId == userId, ct);

    public void Add(WebPushSubscription subscription) => _db.WebPushSubscriptions.Add(subscription);

    public Task RemoveByEndpointAsync(string endpoint, CancellationToken ct) =>
        _db.WebPushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
