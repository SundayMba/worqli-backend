using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servika.Application.Admin;
using Servika.Infrastructure.Notifications;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Hourly (<c>AdminAlerts:SweepMinutes</c>): nudges admins about applications that
/// just crossed the 24h review target, and sends the morning summary once a day at
/// <c>AdminAlerts:DigestHourLagos</c>. Host-agnostic like the other sweeps.
/// </summary>
public sealed class AdminAlertSweepService : BackgroundService
{
    private static readonly TimeSpan Lagos = TimeSpan.FromHours(1); // WAT, no DST

    private readonly IServiceProvider _services;
    private readonly AdminAlertOptions _options;
    private readonly ILogger<AdminAlertSweepService> _logger;
    private DateOnly? _lastDigest;

    public AdminAlertSweepService(IServiceProvider services, AdminAlertOptions options, ILogger<AdminAlertSweepService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.SweepMinutes));
        // Mark today done if we boot after the digest hour, so a redeploy at noon doesn't resend it.
        var bootLocal = DateTimeOffset.UtcNow.ToOffset(Lagos);
        if (bootLocal.Hour > _options.DigestHourLagos) _lastDigest = DateOnly.FromDateTime(bootLocal.DateTime);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<AdminAlertSweepHandler>();
                var overdue = await handler.SendOverdueAsync(interval, stoppingToken);
                if (overdue > 0) _logger.LogInformation("Sent {Count} overdue-application alert(s).", overdue);

                var local = DateTimeOffset.UtcNow.ToOffset(Lagos);
                var today = DateOnly.FromDateTime(local.DateTime);
                if (local.Hour >= _options.DigestHourLagos && _lastDigest != today)
                {
                    _lastDigest = today;
                    await handler.SendDigestAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin alert sweep failed.");
            }
        }
    }
}
