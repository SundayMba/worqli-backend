using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Catalogue;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Deletes verification selfies once they are no longer needed (NDPA 2023): 24h after
/// approval, 30 days after a decline. Runs once on boot, then every
/// <c>Kyc:RetentionSweepMinutes</c> (default 60).
/// </summary>
public sealed class KycImageRetentionService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<KycImageRetentionService> _logger;
    private readonly TimeSpan _interval;

    public KycImageRetentionService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<KycImageRetentionService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Kyc:RetentionSweepMinutes", 60)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<KycImageRetentionHandler>();
                var count = await handler.RunAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Deleted verification selfies for {Count} reviewed application(s).", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KYC selfie retention sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
