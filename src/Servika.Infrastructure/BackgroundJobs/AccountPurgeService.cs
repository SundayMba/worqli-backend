using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Users.Delete;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically hard-erases accounts whose soft-delete grace period has elapsed — the
/// second half of the soft-delete model. An account soft-deleted longer than
/// <c>Purge:GraceDays</c> (default 30) is permanently removed (all rows + all files).
/// Sweep cadence via <c>Purge:SweepHours</c> (default 24).
///
/// Host-agnostic: registered by <c>AddBackgroundSweeps</c> in the API (single instance)
/// or the dedicated <c>Servika.Worker</c> when scaling out, so it runs exactly once.
/// </summary>
public sealed class AccountPurgeService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AccountPurgeService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _graceDays;

    public AccountPurgeService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<AccountPurgeService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromHours(config.GetValue("Purge:SweepHours", 24));
        _graceDays = config.GetValue("Purge:GraceDays", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-_graceDays);
                using var scope = _services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<PurgeDeletedAccountsHandler>();
                var count = await handler.RunAsync(cutoff, stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Purged {Count} soft-deleted account(s) past the grace period.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account purge sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
