using System.Globalization;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Periodically repairs drift caused by something outside the dashboard recreating containers —
/// most notably a Watchtower image update, which gives Gluetun a new container id and leaves the
/// SOCKS5 sidecar bound to a network namespace that no longer exists.
///
/// Interval: GLUETUNWEB_RECONCILE_MINUTES (default 5). Set to 0 to disable.
/// </summary>
public class ContainerReconcilerService(
    IServiceScopeFactory scopeFactory,
    ILogger<ContainerReconcilerService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = ResolveInterval();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval <= TimeSpan.Zero)
        {
            logger.LogInformation("Container reconciler disabled (GLUETUNWEB_RECONCILE_MINUTES=0)");
            return;
        }

        // Small initial delay so startup (migrations, catalog warm-up) settles first.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(_interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var connections = scope.ServiceProvider.GetRequiredService<ConnectionService>();
                var balancers = scope.ServiceProvider.GetRequiredService<LoadBalancerService>();

                var repaired = await connections.ReconcileAsync(stoppingToken)
                               + await balancers.ReconcileAsync(stoppingToken);

                if (repaired > 0)
                    logger.LogInformation("Reconciler repaired {Count} container(s)", repaired);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Container reconciliation pass failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static TimeSpan ResolveInterval()
    {
        // Env vars are always invariant-culture.
        var raw = Environment.GetEnvironmentVariable("GLUETUNWEB_RECONCILE_MINUTES");
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            return m <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(m);
        return TimeSpan.FromMinutes(5);
    }
}
