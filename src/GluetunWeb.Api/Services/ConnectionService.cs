using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;
using GluetunWeb.Api.Gluetun;
using GluetunWeb.Api.Models;
using GluetunWeb.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Owns the full lifecycle of a connection = one Gluetun container plus an optional SOCKS5 sidecar
/// (network_mode: container:&lt;gluetun&gt;). Ports are auto-assigned; secrets are decrypted only here,
/// in-process, and passed to Docker as container env — never returned to the frontend.
/// </summary>
public class ConnectionService(
    AppDbContext db,
    ISecretProtector protector,
    IContainerOrchestrator orchestrator,
    PortManager portManager,
    ContainerOperationLock opLock,
    GluetunControlClient control,
    ProxyTester proxyTester,
    HostEndpointResolver endpoints,
    ILogger<ConnectionService> logger)
{
    // Container-internal listen ports.
    private const int GluetunControlPort = 8000;
    private const int Socks5Port = 1080;
    private const int HttpProxyPort = 8888;

    public string GluetunName(string identifier) => $"gluetunweb-{identifier}";
    public string Socks5Name(string identifier) => $"gluetunweb-{identifier}-socks5";
    /// <summary>Named volume holding this connection's injected config (survives recreation).</summary>
    public string ConfigVolumeName(string identifier) => $"gluetunweb-{identifier}-conf";

    // ---------- Reads ----------

    public async Task<List<ConnectionResponse>> ListAsync(CancellationToken ct = default)
    {
        var conns = await LoadQuery().OrderBy(c => c.Identifier).ToListAsync(ct);
        var result = new List<ConnectionResponse>(conns.Count);
        foreach (var c in conns)
            result.Add(await ToResponseAsync(c, ct));
        return result;
    }

    public async Task<ConnectionResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var c = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : await ToResponseAsync(c, ct);
    }

    public async Task<string?> GetLogsAsync(int id, int tail = 200, CancellationToken ct = default)
    {
        var c = await db.Connections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        if (string.IsNullOrEmpty(c.ContainerId)) return "(container not deployed yet)";
        return await orchestrator.GetLogsAsync(c.ContainerId, tail, ct);
    }

    // ---------- Managed-container ownership detection ----------

    /// <summary>
    /// Lists every container carrying GluetunWeb's label and flags whether it is still "known"
    /// (its connection label matches an existing connection, or its id is tracked). Orphans are
    /// labeled containers whose connection no longer exists (e.g. left behind after a failed delete).
    /// </summary>
    public async Task<List<ManagedContainerResponse>> ListManagedContainersAsync(CancellationToken ct = default)
    {
        var managed = await orchestrator.ListManagedAsync(ct);

        var connections = await db.Connections
            .Select(c => new { c.Identifier, c.ContainerId, c.Socks5ContainerId })
            .ToListAsync(ct);
        var balancers = await db.LoadBalancers
            .Select(l => new { l.Identifier, l.ContainerId })
            .ToListAsync(ct);

        var knownIdentifiers = connections.Select(c => c.Identifier).ToHashSet(StringComparer.Ordinal);
        var knownBalancers = balancers.Select(l => l.Identifier).ToHashSet(StringComparer.Ordinal);
        var trackedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in connections)
        {
            if (!string.IsNullOrEmpty(c.ContainerId)) trackedIds.Add(c.ContainerId);
            if (!string.IsNullOrEmpty(c.Socks5ContainerId)) trackedIds.Add(c.Socks5ContainerId);
        }
        foreach (var l in balancers)
            if (!string.IsNullOrEmpty(l.ContainerId)) trackedIds.Add(l.ContainerId);

        return managed
            .Select(m =>
            {
                var known = trackedIds.Contains(m.Id)
                            || (m.ConnectionLabel is not null && knownIdentifiers.Contains(m.ConnectionLabel))
                            || (m.LoadBalancerLabel is not null && knownBalancers.Contains(m.LoadBalancerLabel));
                return new ManagedContainerResponse(
                    m.Id,
                    m.Id[..Math.Min(12, m.Id.Length)],
                    m.Name,
                    m.Image,
                    m.State,
                    m.ConnectionLabel ?? m.LoadBalancerLabel,
                    known);
            })
            .OrderBy(m => m.Known)
            .ThenBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// Removes a container from Docker. For safety, only containers carrying GluetunWeb's label may
    /// be removed here, so the endpoint can never be used to delete arbitrary host containers.
    /// </summary>
    public async Task RemoveManagedContainerAsync(string id, CancellationToken ct = default)
    {
        var managed = await orchestrator.ListManagedAsync(ct);
        if (managed.All(m => m.Id != id))
            throw new ValidationException("Container is not managed by GluetunWeb (refusing to remove).");

        await orchestrator.RemoveAsync(id, force: true, ct);

        // If it happened to be referenced by a connection, clear the stale reference.
        var conn = await db.Connections.FirstOrDefaultAsync(c => c.ContainerId == id || c.Socks5ContainerId == id, ct);
        if (conn is not null)
        {
            if (conn.ContainerId == id) { conn.ContainerId = null; conn.Status = "created"; }
            if (conn.Socks5ContainerId == id) conn.Socks5ContainerId = null;
            await db.SaveChangesAsync(ct);
        }
    }

    // ---------- CRUD ----------

    public async Task<ConnectionResponse> CreateAsync(ConnectionRequest r, CancellationToken ct = default)
    {
        var source = await ValidateAsync(r, id: null, ct);

        var c = new Connection { Identifier = r.Identifier.Trim() };
        ApplyRequest(c, r, source);
        db.Connections.Add(c);
        await db.SaveChangesAsync(ct);
        // Reserve the port block up front so the assigned ports are visible before the first deploy.
        await portManager.EnsureConnectionBlockAsync(c, ct);
        return await ToResponseAsync((await LoadQuery().FirstAsync(x => x.Id == c.Id, ct)), ct);
    }

    public async Task<ConnectionResponse?> UpdateAsync(int id, ConnectionRequest r, CancellationToken ct = default)
    {
        var c = await db.Connections.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        var source = await ValidateAsync(r, id, ct);

        ApplyRequest(c, r, source);
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync((await LoadQuery().FirstAsync(x => x.Id == id, ct)), ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var c = await db.Connections.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;

        await TeardownContainersAsync(c, ct);
        await orchestrator.RemoveVolumeAsync(ConfigVolumeName(c.Identifier), ct);
        db.Connections.Remove(c);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------- Lifecycle ----------

    public async Task<ConnectionResponse> DeployAsync(int id, CancellationToken ct = default)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var c = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ValidationException("Connection not found.");

        try
        {
            // Clean slate: remove tracked + any orphaned containers with our names.
            await TeardownContainersAsync(c, ct);
            await orchestrator.RemoveByNameAsync(GluetunName(c.Identifier), ct);
            await orchestrator.RemoveByNameAsync(Socks5Name(c.Identifier), ct);

            // Ports (stable across redeploys).
            // One fixed block per connection: each purpose keeps its slot whether or not it is
            // enabled, so toggling a proxy never moves the others.
            var block = await portManager.EnsureConnectionBlockAsync(c, ct);
            var controlPort = PortLayout.PortFor(block, PortPurpose.Control);
            var socksPort = PortLayout.PortFor(block, PortPurpose.Socks5);
            var httpPort = PortLayout.PortFor(block, PortPurpose.HttpProxy);
            var ssPort = PortLayout.PortFor(block, PortPurpose.Shadowsocks);

            var settings = await db.GlobalSettings.AsNoTracking().FirstAsync(ct);
            var build = GluetunConfigBuilder.Build(await BuildInputAsync(c, settings, ct));

            // Gluetun container (owns the shared network namespace + all published ports).
            var gluetunImage = string.IsNullOrWhiteSpace(settings.GluetunImage)
                ? SettingsService.DefaultGluetunImage
                : settings.GluetunImage;
            await orchestrator.EnsureImageAsync(gluetunImage, ct);
            var configVolume = ConfigVolumeName(c.Identifier);
            await orchestrator.EnsureVolumeAsync(configVolume, ct);

            var gluetunSpec = new ContainerSpec
            {
                Image = gluetunImage,
                Name = GluetunName(c.Identifier),
                NeedTun = true,
                Labels = { [DockerLabels.ManagedByKey] = DockerLabels.ManagedByValue, [DockerLabels.ConnectionKey] = c.Identifier },
            };
            foreach (var kv in build.Env) gluetunSpec.Env[kv.Key] = kv.Value;
            // Always mount the config volume so injected files persist across container recreation.
            gluetunSpec.Volumes.Add(new VolumeMount(configVolume, GluetunConfigBuilder.ConfigVolumePath));
            gluetunSpec.Files.AddRange(build.Files);
            gluetunSpec.Ports.Add(new PortMap(controlPort, GluetunControlPort));
            if (c.EnableSocks5) gluetunSpec.Ports.Add(new PortMap(socksPort, Socks5Port));
            if (c.EnableHttpProxy) gluetunSpec.Ports.Add(new PortMap(httpPort, HttpProxyPort));
            if (c.EnableShadowsocks)
            {
                // Shadowsocks serves TCP and UDP on the same port — publish both.
                gluetunSpec.Ports.Add(new PortMap(ssPort, GluetunConfigBuilder.ShadowsocksPort, "tcp"));
                gluetunSpec.Ports.Add(new PortMap(ssPort, GluetunConfigBuilder.ShadowsocksPort, "udp"));
            }

            var gluetunId = await orchestrator.CreateAsync(gluetunSpec, ct);
            await orchestrator.StartAsync(gluetunId, ct);
            c.ContainerId = gluetunId;

            // SOCKS5 sidecar shares the Gluetun netns; the host port was published on Gluetun above.
            c.Socks5ContainerId = c.EnableSocks5
                ? await CreateAndStartSidecarAsync(c, gluetunId, settings, ct)
                : null;

            c.Status = "running";
            c.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return await ToResponseAsync(c, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deploy failed for connection {Identifier}", c.Identifier);
            c.Status = "error";
            await db.SaveChangesAsync(ct);
            throw new ValidationException($"Deploy failed: {ex.Message}");
        }
    }

    public async Task<ConnectionResponse?> StartAsync(int id, CancellationToken ct = default)
        => await LifecycleAsync(id, async c =>
        {
            if (string.IsNullOrEmpty(c.ContainerId))
                throw new ValidationException("Connection is not deployed. Deploy it first.");
            await orchestrator.StartAsync(c.ContainerId, ct);
            if (!string.IsNullOrEmpty(c.Socks5ContainerId)) await orchestrator.StartAsync(c.Socks5ContainerId, ct);
        }, ct);

    public async Task<ConnectionResponse?> StopAsync(int id, CancellationToken ct = default)
        => await LifecycleAsync(id, async c =>
        {
            if (!string.IsNullOrEmpty(c.Socks5ContainerId)) await orchestrator.StopAsync(c.Socks5ContainerId, ct);
            if (!string.IsNullOrEmpty(c.ContainerId)) await orchestrator.StopAsync(c.ContainerId, ct);
        }, ct);

    public async Task<ConnectionResponse?> RestartAsync(int id, CancellationToken ct = default)
    {
        await StopAsync(id, ct);
        return await StartAsync(id, ct);
    }

    private async Task<ConnectionResponse?> LifecycleAsync(int id, Func<Connection, Task> action, CancellationToken ct)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var c = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        await action(c);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(c, ct);
    }

    /// <summary>
    /// Creates and starts the SOCKS5 sidecar joined to the given Gluetun container's network
    /// namespace. Shared by deploy and the reconciler (which relinks it after a recreate).
    /// </summary>
    private async Task<string> CreateAndStartSidecarAsync(
        Connection c, string gluetunId, GlobalSettings settings, CancellationToken ct)
    {
        var socks5Image = string.IsNullOrWhiteSpace(settings.Socks5Image)
            ? SettingsService.DefaultSocks5Image
            : settings.Socks5Image;
        await orchestrator.EnsureImageAsync(socks5Image, ct);

        var socksSpec = new ContainerSpec
        {
            Image = socks5Image,
            Name = Socks5Name(c.Identifier),
            NetworkMode = $"container:{gluetunId}",
            Labels = { [DockerLabels.ManagedByKey] = DockerLabels.ManagedByValue, [DockerLabels.ConnectionKey] = c.Identifier },
        };
        // serjs/go-socks5-proxy requires credentials when REQUIRE_AUTH is true (its default).
        // Provide PROXY_USER/PROXY_PASSWORD when set, otherwise run open with REQUIRE_AUTH=false.
        var socksUser = c.Socks5User;
        var socksPass = protector.Decrypt(c.Socks5PasswordEnc);
        if (!string.IsNullOrWhiteSpace(socksUser) && !string.IsNullOrWhiteSpace(socksPass))
        {
            socksSpec.Env["PROXY_USER"] = socksUser;
            socksSpec.Env["PROXY_PASSWORD"] = socksPass;
            socksSpec.Env["REQUIRE_AUTH"] = "true";
        }
        else
        {
            socksSpec.Env["REQUIRE_AUTH"] = "false";
        }

        var socksId = await orchestrator.CreateAsync(socksSpec, ct);
        await orchestrator.StartAsync(socksId, ct);
        return socksId;
    }

    // ---------- Reconciliation ----------

    /// <summary>
    /// Repairs drift caused by something outside the dashboard recreating containers (e.g. a
    /// Watchtower image update): refreshes the tracked container id, and relinks/restarts the SOCKS5
    /// sidecar when its network namespace points at a Gluetun container that no longer exists.
    /// Deliberately conservative — it never resurrects containers the user removed or stopped.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        using var _lock = await opLock.AcquireAsync(ct);
        var repaired = 0;
        var connections = await db.Connections.Where(c => c.ContainerId != null).ToListAsync(ct);
        if (connections.Count == 0) return 0;
        var settings = await db.GlobalSettings.AsNoTracking().FirstAsync(ct);

        foreach (var c in connections)
        {
            var changed = false;

            // Look the Gluetun container up by name — a recreate keeps the name but changes the id.
            var gluetun = await SafeInspectAsync(GluetunName(c.Identifier), ct);
            if (gluetun is null)
                continue; // genuinely gone — leave it alone (user intent / awaiting redeploy)

            if (!string.Equals(gluetun.Id, c.ContainerId, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Connection {Identifier}: Gluetun container was recreated externally, retracking {Old} -> {New}",
                    c.Identifier, Short(c.ContainerId), Short(gluetun.Id));
                c.ContainerId = gluetun.Id;
                changed = true;
            }

            // Only touch the sidecar while Gluetun is actually running — a container cannot join the
            // netns of a stopped one, and a stopped stack usually means the user stopped it.
            if (c.EnableSocks5 && gluetun.State == "running")
            {
                var expected = $"container:{gluetun.Id}";
                var sidecar = await SafeInspectAsync(Socks5Name(c.Identifier), ct);

                if (sidecar is null || !string.Equals(sidecar.NetworkMode, expected, StringComparison.Ordinal))
                {
                    logger.LogInformation(
                        "Connection {Identifier}: SOCKS5 sidecar netns is stale — recreating against {Gluetun}",
                        c.Identifier, Short(gluetun.Id));
                    await orchestrator.RemoveByNameAsync(Socks5Name(c.Identifier), ct);
                    c.Socks5ContainerId = await CreateAndStartSidecarAsync(c, gluetun.Id, settings, ct);
                    changed = true;
                    repaired++;
                }
                else if (sidecar.State != "running")
                {
                    logger.LogInformation("Connection {Identifier}: restarting stopped SOCKS5 sidecar", c.Identifier);
                    await orchestrator.StartAsync(sidecar.Id, ct);
                    c.Socks5ContainerId = sidecar.Id;
                    changed = true;
                    repaired++;
                }
                else if (!string.Equals(sidecar.Id, c.Socks5ContainerId, StringComparison.Ordinal))
                {
                    c.Socks5ContainerId = sidecar.Id;
                    changed = true;
                }
            }

            if (changed)
            {
                c.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        return repaired;
    }

    private static string Short(string? id) => id is null ? "(none)" : id[..Math.Min(12, id.Length)];

    // ---------- Helpers ----------

    private IQueryable<Connection> LoadQuery()
        => db.Connections
            .Include(c => c.Provider).ThenInclude(p => p!.Credential)
            .Include(c => c.CustomVpnConfig).ThenInclude(x => x!.Credential);

    private async Task TeardownContainersAsync(Connection c, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(c.Socks5ContainerId))
        {
            await orchestrator.RemoveAsync(c.Socks5ContainerId, force: true, ct);
            c.Socks5ContainerId = null;
        }
        if (!string.IsNullOrEmpty(c.ContainerId))
        {
            await orchestrator.RemoveAsync(c.ContainerId, force: true, ct);
            c.ContainerId = null;
        }
        c.Status = "created";
    }

    private async Task<ConnectionSource> ValidateAsync(ConnectionRequest r, int? id, CancellationToken ct)
    {
        var idErr = Identifiers.Validate(r.Identifier);
        if (idErr is not null) throw new ValidationException(idErr);

        var identifier = r.Identifier.Trim();
        if (await db.Connections.AnyAsync(x => x.Identifier == identifier && x.Id != id, ct))
            throw new ValidationException($"A connection named '{identifier}' already exists.");

        if (!Enum.TryParse<ConnectionSource>(r.SourceType, true, out var source))
            throw new ValidationException("Source type must be 'provider' or 'custom'.");

        if (source == ConnectionSource.Provider)
        {
            if (r.ProviderId is null || !await db.Providers.AnyAsync(p => p.Id == r.ProviderId, ct))
                throw new ValidationException("A valid provider must be selected.");
        }
        else
        {
            if (r.CustomVpnConfigId is null || !await db.CustomVpnConfigs.AnyAsync(x => x.Id == r.CustomVpnConfigId, ct))
                throw new ValidationException("A valid custom VPN config must be selected.");
        }

        if (r.EnableShadowsocks && !string.IsNullOrWhiteSpace(r.ShadowsocksCipher)
            && !GluetunConfigBuilder.ShadowsocksCiphers.Contains(r.ShadowsocksCipher.Trim()))
        {
            throw new ValidationException(
                $"Shadowsocks cipher must be one of: {string.Join(", ", GluetunConfigBuilder.ShadowsocksCiphers)}.");
        }

        return source;
    }

    /// <summary>
    /// Shadowsocks has no anonymous mode — Gluetun refuses to start the server without a password,
    /// and an unauthenticated one would be an open relay. Checked after ApplyRequest so an edit that
    /// leaves the password untouched (null = unchanged) still passes on an already-configured row.
    /// </summary>
    private static void ValidateShadowsocksSecret(Connection c)
    {
        if (c.EnableShadowsocks && string.IsNullOrEmpty(c.ShadowsocksPasswordEnc))
            throw new ValidationException("A Shadowsocks password is required when Shadowsocks is enabled.");
    }

    private void ApplyRequest(Connection c, ConnectionRequest r, ConnectionSource source)
    {
        c.Identifier = r.Identifier.Trim();
        c.SourceType = source;
        c.ProviderId = source == ConnectionSource.Provider ? r.ProviderId : null;
        c.CustomVpnConfigId = source == ConnectionSource.Custom ? r.CustomVpnConfigId : null;
        c.ServerCountriesOverride = Blank(r.ServerCountriesOverride);
        c.ServerCitiesOverride = Blank(r.ServerCitiesOverride);
        c.ServerHostnamesOverride = Blank(r.ServerHostnamesOverride);
        c.EnableSocks5 = r.EnableSocks5;
        c.EnableHttpProxy = r.EnableHttpProxy;
        c.Socks5User = Blank(r.Socks5User);
        c.Socks5PasswordEnc = ApplySecret(r.Socks5Password, c.Socks5PasswordEnc);
        c.PortForwarding = r.PortForwarding;
        c.PortForwardingProvider = Blank(r.PortForwardingProvider);
        c.PortForwardingPortsCount = r.PortForwardingPortsCount is >= 1 and <= 5
            ? r.PortForwardingPortsCount
            : 1;
        c.FirewallVpnInputPorts = Blank(r.FirewallVpnInputPorts);
        c.FirewallOutboundSubnets = Blank(r.FirewallOutboundSubnets);
        c.WireGuardMtu = r.WireGuardMtu is > 0 ? r.WireGuardMtu : null;
        c.BlockMalicious = r.BlockMalicious;
        c.BlockAds = r.BlockAds;
        c.DnsUnblockHostnames = Blank(r.DnsUnblockHostnames);
        c.EnableShadowsocks = r.EnableShadowsocks;
        c.ShadowsocksPasswordEnc = ApplySecret(r.ShadowsocksPassword, c.ShadowsocksPasswordEnc);
        c.ShadowsocksCipher = Blank(r.ShadowsocksCipher) ?? GluetunConfigBuilder.DefaultShadowsocksCipher;
        c.ShadowsocksLog = r.ShadowsocksLog;
        ValidateShadowsocksSecret(c);
    }

    // null => unchanged, "" => cleared, otherwise encrypt.
    private string? ApplySecret(string? incoming, string? existingEnc)
    {
        if (incoming is null) return existingEnc;
        if (incoming.Length == 0) return null;
        return protector.Encrypt(incoming);
    }

    /// <summary>Decrypts all secrets and merges global settings + provider/custom + overrides.</summary>
    private async Task<GluetunBuildInput> BuildInputAsync(Connection c, GlobalSettings s, CancellationToken ct)
    {
        var input = new GluetunBuildInput
        {
            Timezone = s.Timezone,
            PublicIpApi = s.PublicIpApi,
            PublicIpApiToken = protector.Decrypt(s.PublicIpApiTokenEnc),
            HttpProxyEnabled = c.EnableHttpProxy,
            HttpProxyUser = s.HttpProxyUser,
            HttpProxyPassword = protector.Decrypt(s.HttpProxyPasswordEnc),
            HttpProxyListeningAddress = s.HttpProxyListeningAddress,
            HttpProxyStealth = s.HttpProxyStealth,
            HttpProxyLog = s.HttpProxyLog,
            PortForwarding = c.PortForwarding,
            PortForwardingProvider = c.PortForwardingProvider,
            PortForwardingPortsCount = c.PortForwardingPortsCount,
            FirewallVpnInputPorts = c.FirewallVpnInputPorts,
            FirewallOutboundSubnets = c.FirewallOutboundSubnets,
            WireGuardMtu = c.WireGuardMtu,
            BlockMalicious = c.BlockMalicious,
            BlockAds = c.BlockAds,
            DnsUnblockHostnames = c.DnsUnblockHostnames,
            ShadowsocksEnabled = c.EnableShadowsocks,
            ShadowsocksPassword = protector.Decrypt(c.ShadowsocksPasswordEnc),
            ShadowsocksCipher = c.ShadowsocksCipher,
            ShadowsocksLog = c.ShadowsocksLog,
            ControlAuth = s.ControlServerAuth,
            ControlUser = s.ControlServerUser,
            ControlPassword = protector.Decrypt(s.ControlServerPasswordEnc),
            ControlApiKey = protector.Decrypt(s.ControlServerApiKeyEnc),
        };

        if (c.SourceType == ConnectionSource.Custom)
        {
            var custom = c.CustomVpnConfig
                         ?? await db.CustomVpnConfigs.Include(x => x.Credential)
                             .FirstAsync(x => x.Id == c.CustomVpnConfigId, ct);
            var raw = protector.Decrypt(custom.RawConfigEnc) ?? string.Empty;
            // Resolve the endpoint DNS name to an IP (Gluetun requires IPs) before the container starts.
            raw = await EndpointResolver.ResolveAndSubstituteAsync(raw, custom.EndpointDnsName, ct);
            // A shared credential wins over the inline fields when one is selected.
            var customCred = await ResolveCredentialAsync(custom.CredentialId, custom.Credential, ct);
            return input with
            {
                IsCustom = true,
                VpnType = custom.VpnType,
                CustomRawConfig = raw,
                OpenVpnUser = protector.Decrypt(customCred?.OpenVpnUserEnc ?? custom.OpenVpnUserEnc),
                OpenVpnPassword = protector.Decrypt(customCred?.OpenVpnPasswordEnc ?? custom.OpenVpnPasswordEnc),
            };
        }

        var p = c.Provider
                ?? await db.Providers.Include(x => x.Credential).FirstAsync(x => x.Id == c.ProviderId, ct);
        var cred = await ResolveCredentialAsync(p.CredentialId, p.Credential, ct);
        return input with
        {
            IsCustom = false,
            VpnType = p.VpnType,
            OpenVpnProtocol = p.OpenVpnProtocol,
            ProviderType = p.ProviderType,
            // A shared credential supplies every secret when selected; otherwise the inline ones do.
            OpenVpnUser = protector.Decrypt(cred?.OpenVpnUserEnc ?? p.OpenVpnUserEnc),
            OpenVpnPassword = protector.Decrypt(cred?.OpenVpnPasswordEnc ?? p.OpenVpnPasswordEnc),
            WireGuardPrivateKey = protector.Decrypt(cred?.WireGuardPrivateKeyEnc ?? p.WireGuardPrivateKeyEnc),
            WireGuardPresharedKey = protector.Decrypt(cred?.WireGuardPresharedKeyEnc ?? p.WireGuardPresharedKeyEnc),
            WireGuardAddresses = cred?.WireGuardAddresses ?? p.WireGuardAddresses,
            ServerCountries = c.ServerCountriesOverride ?? p.ServerCountries,
            ServerCities = c.ServerCitiesOverride ?? p.ServerCities,
            ServerRegions = p.ServerRegions,
            ServerHostnames = c.ServerHostnamesOverride ?? p.ServerHostnames,
        };
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>
    /// Loads the referenced credential, tolerating a caller that did not eager-load it. Returns null
    /// when none is selected, in which case the owner's inline secrets are used.
    /// </summary>
    private async Task<Credential?> ResolveCredentialAsync(
        int? credentialId, Credential? loaded, CancellationToken ct)
    {
        if (credentialId is null) return null;
        return loaded
               ?? await db.Credentials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == credentialId, ct);
    }

    private async Task<ConnectionResponse> ToResponseAsync(Connection c, CancellationToken ct)
    {
        ConnectionRuntime? runtime = null;
        if (!string.IsNullOrEmpty(c.ContainerId))
        {
            var info = await SafeInspectAsync(c.ContainerId, ct);
            if (info is not null)
            {
                // Docker only says whether the process is up. Gluetun retries internally, so a
                // connection that never established still reads "running" here — ask Gluetun itself.
                var vpn = info.State == "running"
                    ? await SafeVpnStateAsync(c, info, ct)
                    : null;

                runtime = new ConnectionRuntime(
                    info.State, info.Health, info.StartedAt,
                    vpn?.VpnStatus, vpn?.PublicIp, vpn?.Country, vpn?.City, vpn?.ForwardedPort, vpn?.Error);

                var mapped = info.State switch
                {
                    "running" => "running",
                    "exited" or "dead" => "exited",
                    "created" => "created",
                    _ => info.State,
                };
                if (c.Status != mapped)
                {
                    c.Status = mapped;
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        return new ConnectionResponse(
            c.Id,
            c.Identifier,
            c.SourceType.ToString().ToLowerInvariant(),
            c.ProviderId,
            c.Provider?.Name,
            c.CustomVpnConfigId,
            c.CustomVpnConfig?.Name,
            c.ServerCountriesOverride,
            c.ServerCitiesOverride,
            c.ServerHostnamesOverride,
            c.EnableSocks5,
            c.EnableHttpProxy,
            c.Socks5User,
            !string.IsNullOrEmpty(c.Socks5PasswordEnc),
            PortLayout.PortFor(c.PortBlockStart, PortPurpose.Socks5),
            PortLayout.PortFor(c.PortBlockStart, PortPurpose.HttpProxy),
            c.PortForwarding,
            c.PortForwardingProvider,
            c.PortForwardingPortsCount,
            c.FirewallVpnInputPorts,
            c.FirewallOutboundSubnets,
            c.WireGuardMtu,
            c.BlockMalicious,
            c.BlockAds,
            c.DnsUnblockHostnames,
            c.EnableShadowsocks,
            !string.IsNullOrEmpty(c.ShadowsocksPasswordEnc),
            c.ShadowsocksCipher,
            c.ShadowsocksLog,
            PortLayout.PortFor(c.PortBlockStart, PortPurpose.Shadowsocks),
            PortLayout.PortFor(c.PortBlockStart, PortPurpose.Control),
            c.PortBlockStart,
            c.PortBlockStart > 0 ? PortLayout.BlockEnd(c.PortBlockStart, PortLayout.ConnectionBlockSize) : 0,
            c.Status,
            c.ContainerId is null ? null : c.ContainerId[..Math.Min(12, c.ContainerId.Length)],
            runtime,
            c.CreatedAt,
            c.UpdatedAt);
    }

    /// <summary>
    private async Task<GluetunVpnState?> SafeVpnStateAsync(
        Connection c, ContainerRuntimeInfo info, CancellationToken ct)
    {
        try
        {
            var settings = await db.GlobalSettings.AsNoTracking().FirstAsync(ct);
            var endpoint = await endpoints.ResolveAsync(
                info.Id, info.IpAddress, GluetunControlPort,
                PortLayout.PortFor(c.PortBlockStart, PortPurpose.Control), ct);
            if (endpoint is null)
                return null; // control server unreachable — degrades to "vpn unknown" in the UI

            var (host, port) = endpoint.Value;
            return await control.GetStateAsync(
                cacheKey: $"conn-{c.Id}",
                baseUrl: $"http://{host}:{port}",
                settings,
                settings.ControlServerUser,
                protector.Decrypt(settings.ControlServerPasswordEnc),
                protector.Decrypt(settings.ControlServerApiKeyEnc),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read Gluetun state for {Identifier}", c.Identifier);
            return null;
        }
    }

    /// <summary>Fetches a URL through this connection's SOCKS5 proxy.</summary>
    public async Task<ProxyTestResponse> TestAsync(int id, string? url, CancellationToken ct = default)
    {
        var c = await db.Connections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ValidationException("Connection not found.");

        if (!c.EnableSocks5)
            throw new ValidationException("Enable the SOCKS5 proxy on this connection to test it.");
        if (string.IsNullOrEmpty(c.ContainerId))
            throw new ValidationException("Deploy this connection before testing it.");

        var info = await SafeInspectAsync(c.ContainerId, ct);
        if (info is null || info.State != "running")
            throw new ValidationException("The connection is not running — start it before testing.");

        // Ask Gluetun whether the tunnel is actually up before spending 20s discovering it is not.
        // A container can be "running" while OpenVPN retries a rejected login indefinitely, and the
        // public IP is only obtained after a real connection — so its absence is the reliable tell.
        var vpn = await SafeVpnStateAsync(c, info, ct);
        if (vpn is not null && vpn.PublicIp is null && vpn.Error is null)
        {
            var detail = vpn.VpnStatus is null
                ? "Gluetun's control server did not answer."
                : $"Gluetun reports the VPN process as '{vpn.VpnStatus}', but no public IP has been "
                  + "obtained — so the tunnel has not established.";
            throw new ValidationException(
                $"{detail} Traffic through the proxy would go nowhere. Check the connection's logs "
                + "for the reason (bad credentials, no matching server, TUN device).");
        }

        var endpoint = await endpoints.ResolveAsync(
            info.Id, info.IpAddress, Socks5Port,
            PortLayout.PortFor(c.PortBlockStart, PortPurpose.Socks5), ct);
        if (endpoint is null)
        {
            throw new ValidationException(
                "Could not reach the SOCKS5 port from GluetunWeb — tried the published port on the "
                + "host and the container's own address. If GluetunWeb runs in a container, make sure "
                + "it can reach the host (the compose file maps host.docker.internal).");
        }

        var (host, port) = endpoint.Value;
        return await proxyTester.TestAsync(
            host, port, c.Socks5User, protector.Decrypt(c.Socks5PasswordEnc),
            string.IsNullOrWhiteSpace(url) ? DefaultTestUrl : url!, ct);
    }

    /// <summary>Default target for the connectivity test.</summary>
    public const string DefaultTestUrl = "https://ipwho.is/";

    private async Task<ContainerRuntimeInfo?> SafeInspectAsync(string id, CancellationToken ct)
    {
        try { return await orchestrator.InspectAsync(id, ct); }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Inspect failed for {Id}", id);
            return null;
        }
    }
}
