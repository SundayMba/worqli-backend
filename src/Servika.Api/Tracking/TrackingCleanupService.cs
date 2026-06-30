using Microsoft.AspNetCore.SignalR;
using Servika.Api.Hubs;
using Servika.Application.Tracking;

namespace Servika.Api.Tracking;

/// <summary>
/// Sweeps up live-tracking sessions that have gone quiet — an artisan who closed
/// the app or lost signal mid-trip leaves an <c>Active</c> session that would
/// otherwise linger. Periodically ends sessions with no recent update and notifies
/// their groups with <c>TrackingEnded</c>.
///
/// Hosted in the API for this slice so it runs alongside the hub and is easy to
/// verify; its natural home is the dedicated <c>Servika.Worker</c> process. Cadence
/// and staleness window are configurable via <c>Tracking:SweepSeconds</c> /
/// <c>Tracking:StaleAfterSeconds</c> (defaults 60s / 180s).
/// </summary>
public sealed class TrackingCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHubContext<TrackingHub> _hub;
    private readonly ILogger<TrackingCleanupService> _logger;
    private readonly TimeSpan _sweepInterval;
    private readonly TimeSpan _staleAfter;

    public TrackingCleanupService(
        IServiceProvider services,
        IHubContext<TrackingHub> hub,
        IConfiguration config,
        ILogger<TrackingCleanupService> logger)
    {
        _services = services;
        _hub = hub;
        _logger = logger;
        _sweepInterval = TimeSpan.FromSeconds(config.GetValue("Tracking:SweepSeconds", 60));
        _staleAfter = TimeSpan.FromSeconds(config.GetValue("Tracking:StaleAfterSeconds", 180));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_sweepInterval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed sweep must never take the host down — log and retry next tick.
                _logger.LogError(ex, "Tracking cleanup sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        // Scoped DI: the TrackingService (and its EF DbContext) is per-operation.
        using var scope = _services.CreateScope();
        var tracking = scope.ServiceProvider.GetRequiredService<TrackingService>();

        var endedBookingIds = await tracking.EndStaleAsync(_staleAfter, ct);
        foreach (var bookingId in endedBookingIds)
        {
            await _hub.Clients
                .Group($"booking:{bookingId}")
                .SendAsync("TrackingEnded", new { bookingId, reason = "stale" }, ct);
        }

        if (endedBookingIds.Count > 0)
            _logger.LogInformation("Ended {Count} stale tracking session(s).", endedBookingIds.Count);
    }
}
