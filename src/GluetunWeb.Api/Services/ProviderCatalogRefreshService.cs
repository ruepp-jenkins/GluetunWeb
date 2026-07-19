using System.Globalization;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Warms up the provider catalog at startup and then git-pulls it on a schedule
/// (GLUETUNWEB_SERVERS_REFRESH_HOURS, default 6h) so provider names stay current.
/// </summary>
public class ProviderCatalogRefreshService(
    ProviderCatalogService catalog,
    ILogger<ProviderCatalogRefreshService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = ResolveInterval();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Warm-up clone at startup (non-blocking for the app; ready before the first UI visit).
        try
        {
            var initial = await catalog.GetAsync(forceRefresh: false, stoppingToken);
            logger.LogInformation("Provider catalog loaded: {Count} providers ({Source})",
                initial.Providers.Count, initial.Source);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { logger.LogWarning(ex, "Initial provider catalog load failed"); }

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var c = await catalog.GetAsync(forceRefresh: true, stoppingToken);
                logger.LogInformation("Provider catalog refreshed: {Count} providers ({Source})",
                    c.Providers.Count, c.Source);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Scheduled provider catalog refresh failed"); }
        }
    }

    private static TimeSpan ResolveInterval()
    {
        // Env vars are always invariant-culture (a German-locale host must still parse "0.002"/"2.5").
        var raw = Environment.GetEnvironmentVariable("GLUETUNWEB_SERVERS_REFRESH_HOURS");
        var hours = double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var h) && h > 0
            ? h
            : 6.0;
        return TimeSpan.FromHours(hours);
    }
}
