using Microsoft.Extensions.DependencyInjection;

namespace Servika.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the marketplace's periodic background sweeps. Called by whichever host
/// owns them: the API (single-instance dev/launch) or the dedicated
/// <c>Servika.Worker</c> when the API is scaled out — so the sweeps run exactly once,
/// never once per API instance.
/// </summary>
public static class BackgroundJobsExtensions
{
    public static IServiceCollection AddBackgroundSweeps(this IServiceCollection services)
    {
        services.AddHostedService<TrackingCleanupService>();
        services.AddHostedService<CompletionAutoConfirmService>();
        return services;
    }
}
