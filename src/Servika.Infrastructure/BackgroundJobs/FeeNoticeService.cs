using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Payments;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Hourly sweep that sends the advance notices before transaction fees start
/// (30 / 7 / 1 days, then the day itself). Cadence via <c>Fees:NoticeSweepMinutes</c>
/// (default 60). Runs once on boot, then on the timer. Host-agnostic like the other sweeps.
/// </summary>
public sealed class FeeNoticeService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FeeNoticeService> _logger;
    private readonly TimeSpan _interval;

    public FeeNoticeService(IServiceProvider services, IConfiguration config, ILogger<FeeNoticeService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(config.GetValue("Fees:NoticeSweepMinutes", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<SendFeeNoticesHandler>();
                var count = await handler.RunAsync(stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Sent {Count} transaction-fee notice(s).", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fee notice sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
