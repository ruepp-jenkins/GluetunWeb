using GluetunWeb.Api.Balancer;
using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;
using GluetunWeb.Api.Models;
using GluetunWeb.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Owns the lifecycle of Socks5BalancerAsio (ruepp/socks5balancerasio) containers that load-balance
/// across the SOCKS5 proxies of selected connections. The config.json is generated from the balancer
/// settings + upstreams and injected at /app/config.json on deploy.
/// </summary>
public class LoadBalancerService(
    AppDbContext db,
    ISecretProtector protector,
    IContainerOrchestrator orchestrator,
    PortManager portManager,
    ContainerOperationLock opLock,
    ProxyTester proxyTester,
    HostEndpointResolver endpoints,
    ILogger<LoadBalancerService> logger)
{
    public string ContainerName(string identifier) => $"gluetunweb-lb-{identifier}";
    /// <summary>Named volume holding this balancer's config.json (survives recreation).</summary>
    public string ConfigVolumeName(string identifier) => $"gluetunweb-lb-{identifier}-conf";

    /// <summary>Fetches a URL through this balancer's balanced SOCKS5 listener.</summary>
    public async Task<ProxyTestResponse> TestAsync(int id, string? url, CancellationToken ct = default)
    {
        var l = await db.LoadBalancers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ValidationException("Load balancer not found.");

        if (string.IsNullOrEmpty(l.ContainerId))
            throw new ValidationException("Deploy this load balancer before testing it.");

        ContainerRuntimeInfo? info;
        try { info = await orchestrator.InspectAsync(l.ContainerId, ct); }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Inspect failed for balancer {Id}", l.ContainerId);
            info = null;
        }
        if (info is null || info.State != "running")
            throw new ValidationException("The load balancer is not running — start it before testing.");

        // Reach the balanced listener at whichever address actually works from here — the published
        // port on the host when GluetunWeb runs in a container, or the container's own IP on the
        // same network. See HostEndpointResolver.
        var endpoint = await endpoints.ResolveAsync(
            info.Id, info.IpAddress, Socks5BalancerConfigBuilder.ListenPort,
            PortLayout.PortFor(l.PortBlockStart, PortPurpose.BalancerListen), ct);
        if (endpoint is null)
        {
            throw new ValidationException(
                "Could not reach the balancer's listen port from GluetunWeb — tried the published "
                + "port on the host and the container's own address.");
        }

        var (host, port) = endpoint.Value;
        // The balancer authenticates to its upstreams itself; clients connect to it without creds.
        return await proxyTester.TestAsync(
            host, port, null, null,
            string.IsNullOrWhiteSpace(url) ? ConnectionService.DefaultTestUrl : url!, ct);
    }

    // ---------- Reads ----------

    public async Task<List<LoadBalancerResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await LoadQuery().OrderBy(l => l.Identifier).ToListAsync(ct);
        var result = new List<LoadBalancerResponse>(items.Count);
        foreach (var l in items) result.Add(await ToResponseAsync(l, ct));
        return result;
    }

    public async Task<LoadBalancerResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var l = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        return l is null ? null : await ToResponseAsync(l, ct);
    }

    public async Task<string?> GetLogsAsync(int id, int tail = 200, CancellationToken ct = default)
    {
        var l = await db.LoadBalancers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;
        if (string.IsNullOrEmpty(l.ContainerId)) return "(balancer not deployed yet)";
        return await orchestrator.GetLogsAsync(l.ContainerId, tail, ct);
    }

    // ---------- CRUD ----------

    public async Task<LoadBalancerResponse> CreateAsync(LoadBalancerRequest r, CancellationToken ct = default)
    {
        await ValidateAsync(r, id: null, ct);
        var l = new LoadBalancer { Identifier = r.Identifier.Trim() };
        ApplyRequest(l, r);
        ReplaceUpstreams(l, r.ConnectionIds);
        db.LoadBalancers.Add(l);
        await db.SaveChangesAsync(ct);
        // Reserve the port block up front so the assigned ports are visible before the first deploy.
        await portManager.EnsureBalancerBlockAsync(l, ct);
        return await ToResponseAsync(await LoadQuery().FirstAsync(x => x.Id == l.Id, ct), ct);
    }

    public async Task<LoadBalancerResponse?> UpdateAsync(int id, LoadBalancerRequest r, CancellationToken ct = default)
    {
        var l = await db.LoadBalancers.Include(x => x.Upstreams).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;
        await ValidateAsync(r, id, ct);
        ApplyRequest(l, r);
        ReplaceUpstreams(l, r.ConnectionIds);
        l.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(await LoadQuery().FirstAsync(x => x.Id == id, ct), ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var l = await db.LoadBalancers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return false;
        await TeardownAsync(l, ct);
        await orchestrator.RemoveVolumeAsync(ConfigVolumeName(l.Identifier), ct);
        db.LoadBalancers.Remove(l);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------- Lifecycle ----------

    public async Task<LoadBalancerResponse> DeployAsync(int id, CancellationToken ct = default)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var l = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ValidationException("Load balancer not found.");

        var upstreams = l.Upstreams
            .Select(u => u.Connection!)
            .Where(c => c is not null && c.EnableSocks5)
            .ToList();
        if (upstreams.Count == 0)
            throw new ValidationException("Add at least one SOCKS5-enabled connection as an upstream before deploying.");

        try
        {
            await TeardownAsync(l, ct);
            await orchestrator.RemoveByNameAsync(ContainerName(l.Identifier), ct);

            // One fixed block per balancer; listen/web/state sit at fixed offsets inside it.
            var block = await portManager.EnsureBalancerBlockAsync(l, ct);
            var listenPort = PortLayout.PortFor(block, PortPurpose.BalancerListen);
            var webPort = PortLayout.PortFor(block, PortPurpose.BalancerWeb);
            var statePort = PortLayout.PortFor(block, PortPurpose.BalancerState);

            var settings = await db.GlobalSettings.AsNoTracking().FirstAsync(ct);
            var config = Socks5BalancerConfigBuilder.Build(BuildConfigInput(l, upstreams));

            var balancerImage = string.IsNullOrWhiteSpace(settings.Socks5BalancerImage)
                ? SettingsService.DefaultSocks5BalancerImage
                : settings.Socks5BalancerImage;
            await orchestrator.EnsureImageAsync(balancerImage, ct);
            var configVolume = ConfigVolumeName(l.Identifier);
            await orchestrator.EnsureVolumeAsync(configVolume, ct);

            var spec = new ContainerSpec
            {
                Image = balancerImage,
                Name = ContainerName(l.Identifier),
                Labels = { [DockerLabels.ManagedByKey] = DockerLabels.ManagedByValue, [DockerLabels.LoadBalancerKey] = l.Identifier },
                // host.docker.internal lets the balancer reach the host-published SOCKS5 ports.
                ExtraHosts = { "host.docker.internal:host-gateway" },
                // The image's default command reads ./config.json from /app; point it at the volume instead.
                Command = new[] { "./Socks5BalancerAsio", Socks5BalancerConfigBuilder.ConfigPath },
            };
            spec.Volumes.Add(new VolumeMount(configVolume, Socks5BalancerConfigBuilder.ConfigVolumePath));
            spec.Files.Add(new FileToCopy(Socks5BalancerConfigBuilder.ConfigPath, config));
            spec.Ports.Add(new PortMap(listenPort, Socks5BalancerConfigBuilder.ListenPort));
            spec.Ports.Add(new PortMap(webPort, Socks5BalancerConfigBuilder.WebPort));
            spec.Ports.Add(new PortMap(statePort, Socks5BalancerConfigBuilder.StateServerPort));

            var containerId = await orchestrator.CreateAsync(spec, ct);
            await orchestrator.StartAsync(containerId, ct);
            l.ContainerId = containerId;
            l.Status = "running";
            l.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return await ToResponseAsync(l, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deploy failed for load balancer {Identifier}", l.Identifier);
            l.Status = "error";
            await db.SaveChangesAsync(ct);
            throw new ValidationException($"Deploy failed: {ex.Message}");
        }
    }

    public Task<LoadBalancerResponse?> StartAsync(int id, CancellationToken ct = default)
        => LifecycleAsync(id, async l =>
        {
            if (string.IsNullOrEmpty(l.ContainerId))
                throw new ValidationException("Load balancer is not deployed. Deploy it first.");
            await orchestrator.StartAsync(l.ContainerId, ct);
        }, ct);

    public Task<LoadBalancerResponse?> StopAsync(int id, CancellationToken ct = default)
        => LifecycleAsync(id, async l =>
        {
            if (!string.IsNullOrEmpty(l.ContainerId)) await orchestrator.StopAsync(l.ContainerId, ct);
        }, ct);

    public async Task<LoadBalancerResponse?> RestartAsync(int id, CancellationToken ct = default)
    {
        await StopAsync(id, ct);
        return await StartAsync(id, ct);
    }

    private async Task<LoadBalancerResponse?> LifecycleAsync(int id, Func<LoadBalancer, Task> action, CancellationToken ct)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var l = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;
        await action(l);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(l, ct);
    }

    /// <summary>
    /// Balancers have no netns coupling and their config lives on a volume, so a recreate comes back
    /// healthy — but the tracked container id goes stale. Refresh it by name.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var repaired = 0;
        var balancers = await db.LoadBalancers.Where(l => l.ContainerId != null).ToListAsync(ct);

        foreach (var l in balancers)
        {
            ContainerRuntimeInfo? info;
            try { info = await orchestrator.InspectAsync(ContainerName(l.Identifier), ct); }
            catch { continue; }

            if (info is null) continue; // gone — leave alone
            if (string.Equals(info.Id, l.ContainerId, StringComparison.Ordinal)) continue;

            logger.LogInformation(
                "Load balancer {Identifier}: container recreated externally, retracking {Old} -> {New}",
                l.Identifier,
                l.ContainerId?[..Math.Min(12, l.ContainerId.Length)],
                info.Id[..Math.Min(12, info.Id.Length)]);
            l.ContainerId = info.Id;
            l.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            repaired++;
        }

        return repaired;
    }

    // ---------- Helpers ----------

    private IQueryable<LoadBalancer> LoadQuery()
        => db.LoadBalancers.Include(l => l.Upstreams).ThenInclude(u => u.Connection);

    private async Task TeardownAsync(LoadBalancer l, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(l.ContainerId))
        {
            await orchestrator.RemoveAsync(l.ContainerId, force: true, ct);
            l.ContainerId = null;
        }
        l.Status = "created";
    }

    private async Task ValidateAsync(LoadBalancerRequest r, int? id, CancellationToken ct)
    {
        var idErr = Identifiers.Validate(r.Identifier);
        if (idErr is not null) throw new ValidationException(idErr);
        var identifier = r.Identifier.Trim();
        if (await db.LoadBalancers.AnyAsync(x => x.Identifier == identifier && x.Id != id, ct))
            throw new ValidationException($"A load balancer named '{identifier}' already exists.");

        if (r.ConnectionIds.Count > 0)
        {
            var valid = await db.Connections.Where(c => r.ConnectionIds.Contains(c.Id) && c.EnableSocks5)
                .Select(c => c.Id).ToListAsync(ct);
            var invalid = r.ConnectionIds.Except(valid).ToList();
            if (invalid.Count > 0)
                throw new ValidationException("All upstreams must be existing connections with SOCKS5 enabled.");
        }
    }

    private static void ApplyRequest(LoadBalancer l, LoadBalancerRequest r)
    {
        l.Identifier = r.Identifier.Trim();
        l.UpstreamHost = string.IsNullOrWhiteSpace(r.UpstreamHost) ? "host.docker.internal" : r.UpstreamHost.Trim();
        l.UpstreamSelectRule = string.IsNullOrWhiteSpace(r.UpstreamSelectRule) ? "loop" : r.UpstreamSelectRule.Trim();
        l.RetryTimes = r.RetryTimes;
        l.ConnectTimeout = r.ConnectTimeout;
        l.TestRemoteHost = string.IsNullOrWhiteSpace(r.TestRemoteHost) ? "www.google.com" : r.TestRemoteHost.Trim();
        l.TestRemotePort = r.TestRemotePort;
        l.TcpCheckPeriod = r.TcpCheckPeriod;
        l.ConnectCheckPeriod = r.ConnectCheckPeriod;
        l.AdditionCheckPeriod = r.AdditionCheckPeriod;
        l.ThreadNum = r.ThreadNum;
        l.ServerChangeTime = r.ServerChangeTime;
    }

    /// <summary>
    /// Reconciles the balancer's upstream set against <paramref name="connectionIds"/> through the
    /// change tracker (requires the Upstreams collection to be loaded). Removing an upstream orphans
    /// the required child, which EF deletes on save. Using bulk ExecuteDelete here would double-delete
    /// the already-tracked rows and throw a concurrency exception.
    /// </summary>
    internal static void ReplaceUpstreams(LoadBalancer l, List<int> connectionIds)
    {
        var desired = connectionIds.Distinct().ToList();
        var desiredSet = desired.ToHashSet();

        foreach (var stale in l.Upstreams.Where(u => !desiredSet.Contains(u.ConnectionId)).ToList())
            l.Upstreams.Remove(stale);

        var existing = l.Upstreams.Select(u => u.ConnectionId).ToHashSet();
        foreach (var cid in desired.Where(c => !existing.Contains(c)))
            l.Upstreams.Add(new LoadBalancerUpstream { ConnectionId = cid });
    }

    private BalancerConfigInput BuildConfigInput(LoadBalancer l, List<Connection> upstreams) => new()
    {
        UpstreamSelectRule = l.UpstreamSelectRule,
        RetryTimes = l.RetryTimes,
        ConnectTimeout = l.ConnectTimeout,
        TestRemoteHost = l.TestRemoteHost,
        TestRemotePort = l.TestRemotePort,
        TcpCheckPeriod = l.TcpCheckPeriod,
        ConnectCheckPeriod = l.ConnectCheckPeriod,
        AdditionCheckPeriod = l.AdditionCheckPeriod,
        ThreadNum = l.ThreadNum,
        ServerChangeTime = l.ServerChangeTime,
        Upstreams = upstreams.Select(c => new BalancerUpstreamInput(
            c.Identifier,
            l.UpstreamHost,
            PortLayout.PortFor(c.PortBlockStart, PortPurpose.Socks5),
            c.Socks5User,
            protector.Decrypt(c.Socks5PasswordEnc))).ToList(),
    };

    private async Task<LoadBalancerResponse> ToResponseAsync(LoadBalancer l, CancellationToken ct)
    {
        ConnectionRuntime? runtime = null;
        if (!string.IsNullOrEmpty(l.ContainerId))
        {
            try
            {
                var info = await orchestrator.InspectAsync(l.ContainerId, ct);
                if (info is not null)
                {
                    runtime = new ConnectionRuntime(info.State, info.Health, info.StartedAt);
                    var mapped = info.State is "running" or "exited" or "created" ? info.State : info.State;
                    if (l.Status != mapped) { l.Status = mapped; await db.SaveChangesAsync(ct); }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Inspect failed for balancer {Id}", l.ContainerId);
            }
        }

        var upstreams = l.Upstreams
            .Where(u => u.Connection is not null)
            .Select(u => new LoadBalancerUpstreamResponse(
                u.ConnectionId,
                u.Connection!.Identifier,
                PortLayout.PortFor(u.Connection.PortBlockStart, PortPurpose.Socks5),
                !string.IsNullOrEmpty(u.Connection.ContainerId)))
            .OrderBy(u => u.Identifier)
            .ToList();

        return new LoadBalancerResponse(
            l.Id, l.Identifier, l.UpstreamHost, l.UpstreamSelectRule,
            l.RetryTimes, l.ConnectTimeout, l.TestRemoteHost, l.TestRemotePort,
            l.TcpCheckPeriod, l.ConnectCheckPeriod, l.AdditionCheckPeriod, l.ThreadNum, l.ServerChangeTime,
            PortLayout.PortFor(l.PortBlockStart, PortPurpose.BalancerListen),
            PortLayout.PortFor(l.PortBlockStart, PortPurpose.BalancerWeb),
            PortLayout.PortFor(l.PortBlockStart, PortPurpose.BalancerState),
            l.PortBlockStart,
            l.PortBlockStart > 0 ? PortLayout.BlockEnd(l.PortBlockStart, PortLayout.BalancerBlockSize) : 0,
            l.Status,
            l.ContainerId is null ? null : l.ContainerId[..Math.Min(12, l.ContainerId.Length)],
            runtime,
            upstreams,
            l.CreatedAt, l.UpdatedAt);
    }
}
