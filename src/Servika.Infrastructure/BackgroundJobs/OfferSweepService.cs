using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Bookings;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Keeps job offers moving: expires the ones nobody answered within the window and offers the job
/// to the next artisan, and retries requests that found nobody. Cadence via
/// <c>Offers:SweepSeconds</c> (default 30). Hosted like the other sweeps (API or Worker, once).
/// </summary>
public sealed class OfferSweepService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OfferSweepService> _logger;
    private readonly TimeSpan _interval;

    public OfferSweepService(IServiceProvider services, IConfiguration config, ILogger<OfferSweepService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(5, config.GetValue("Offers:SweepSeconds", 30)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<OfferSweepHandler>();
                var (expired, retried) = await handler.RunAsync(stoppingToken);
                if (expired > 0 || retried > 0)
                    _logger.LogInformation("Offer sweep: {Expired} expired offer(s) moved on, {Retried} request(s) retried.", expired, retried);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Offer sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
