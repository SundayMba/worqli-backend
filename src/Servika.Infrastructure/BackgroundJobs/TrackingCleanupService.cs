using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Tracking;
using Servika.Application.Tracking;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Sweeps up live-tracking sessions that have gone quiet — an artisan who closed
/// the app or lost signal mid-trip leaves an <c>Active</c> session that would
/// otherwise linger. Periodically ends stale sessions and (best-effort) tells their
/// tracking group with <c>TrackingEnded</c>.
///
/// Host-agnostic: registered by <c>AddBackgroundSweeps</c> in the API or the
/// <c>Servika.Worker</c>. The DB cleanup runs wherever it's hosted; the
/// <c>TrackingEnded</c> broadcast goes out only when an
/// <see cref="ITrackingRealtimePublisher"/> is registered (the API, which owns the
/// hub) — in the worker it's absent and the broadcast is skipped (cross-process
/// delivery needs a SignalR Redis backplane, not wired yet). Cadence + staleness via
/// <c>Tracking:SweepSeconds</c> / <c>Tracking:StaleAfterSeconds</c> (defaults 60/180).
/// </summary>
public sealed class TrackingCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TrackingCleanupService> _logger;
    private readonly TimeSpan _sweepInterval;
    private readonly TimeSpan _staleAfter;

    public TrackingCleanupService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<TrackingCleanupService> logger)
    {
        _services = services;
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
        // Optional — present only in the host that owns the tracking hub (the API).
        var realtime = scope.ServiceProvider.GetService<ITrackingRealtimePublisher>();

        var endedBookingIds = await tracking.EndStaleAsync(_staleAfter, ct);
        if (realtime is not null)
        {
            foreach (var bookingId in endedBookingIds)
                await realtime.TrackingEndedAsync(bookingId, "stale", ct);
        }

        if (endedBookingIds.Count > 0)
            _logger.LogInformation("Ended {Count} stale tracking session(s).", endedBookingIds.Count);
    }
}
