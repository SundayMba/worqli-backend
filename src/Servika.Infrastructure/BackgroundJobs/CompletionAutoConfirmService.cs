using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Bookings;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically auto-confirms jobs the customer never confirmed. A job that has sat
/// AwaitingConfirmation (artisan submitted proof) past the grace window is completed
/// automatically, so an unresponsive customer can't strand the artisan. Cadence via
/// <c>Completion:SweepMinutes</c> (default 30).
///
/// Host-agnostic: registered by <c>AddBackgroundSweeps</c> in either the API (single
/// instance) or the dedicated <c>Servika.Worker</c> (when scaling the API out, so the
/// sweep runs exactly once).
/// </summary>
public sealed class CompletionAutoConfirmService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CompletionAutoConfirmService> _logger;
    private readonly TimeSpan _interval;

    public CompletionAutoConfirmService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<CompletionAutoConfirmService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(config.GetValue("Completion:SweepMinutes", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<AutoConfirmCompletionsHandler>();
                var count = await handler.RunAsync(stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Auto-confirmed {Count} completed job(s).", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Completion auto-confirm sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
