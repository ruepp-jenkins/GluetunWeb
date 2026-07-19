using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Works out an address at which GluetunWeb can actually reach a managed container's port, which is
/// not the same as the container's own bridge IP:
///
/// - GluetunWeb on the host reaches the <b>published</b> port at <c>localhost</c>.
/// - GluetunWeb in a container reaches the published port at its own <b>default gateway</b> (the
///   host), regardless of whether the target is on the same Docker network — the port is bound on
///   the host, not just inside the target's namespace.
/// - Only when the two containers share a network does the target's <b>internal</b> port on its own
///   IP work, and that is the case the old code assumed exclusively.
///
/// So rather than guess from one signal, this probes the candidates in order and returns the first
/// that accepts a TCP connection. Results are cached per container id (a redeploy changes the id, so
/// a stale entry is naturally replaced) and re-validated cheaply on reuse.
/// </summary>
public class HostEndpointResolver(ILogger<HostEndpointResolver> logger)
{
    // Docker (and Podman) create this marker file inside every container.
    private static readonly bool InContainer = File.Exists("/.dockerenv") || File.Exists("/run/.containerenv");

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, (string Host, int Port)> _cache = new();

    /// <summary>
    /// The first reachable (host, port) for a container's service, or null if none answered.
    /// <paramref name="containerIp"/> may be null (a sidecar has no IP of its own).
    /// </summary>
    public async Task<(string Host, int Port)?> ResolveAsync(
        string containerId, string? containerIp, int internalPort, int publishedPort,
        CancellationToken ct = default)
    {
        var key = $"{containerId}:{internalPort}";
        if (_cache.TryGetValue(key, out var cached) && await CanConnectAsync(cached.Host, cached.Port, ct))
            return cached;

        foreach (var candidate in Candidates(containerIp, internalPort, publishedPort))
        {
            if (await CanConnectAsync(candidate.Host, candidate.Port, ct))
            {
                _cache[key] = candidate;
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Resolved {Container} port {Internal} to {Host}:{Port}",
                        containerId[..Math.Min(12, containerId.Length)], internalPort, candidate.Host, candidate.Port);
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Candidate addresses, most-portable first. Every one is safe: the published port is unique to
    /// this container (the port manager guarantees no collision), so hitting it on any host interface
    /// always reaches the intended service.
    /// </summary>
    private static IEnumerable<(string Host, int Port)> Candidates(string? ip, int internalPort, int published)
    {
        yield return ("localhost", published);

        if (InContainer)
        {
            var gateway = DefaultGateway();
            if (gateway is not null)
                yield return (gateway, published);
            // Works only when the container was given `--add-host host.docker.internal:host-gateway`,
            // but harmless to try otherwise (it simply fails to resolve/connect).
            yield return ("host.docker.internal", published);
        }

        // Last resort: the two containers share a Docker network, so the in-namespace port is reachable.
        if (!string.IsNullOrWhiteSpace(ip))
            yield return (ip!, internalPort);
    }

    private static async Task<bool> CanConnectAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ProbeTimeout);
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// This container's IPv4 default gateway from /proc/net/route — the host, as reachable from
    /// inside Docker. Null on non-Linux or when the table cannot be read.
    /// </summary>
    private static string? DefaultGateway()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/net/route").Skip(1))
            {
                var f = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (f.Length <= 2 || f[1] != "00000000")
                    continue;

                // Gateway is little-endian hex, e.g. "010012AC" => 172.18.0.1.
                var raw = uint.Parse(f[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return $"{raw & 0xFF}.{(raw >> 8) & 0xFF}.{(raw >> 16) & 0xFF}.{(raw >> 24) & 0xFF}";
            }
        }
        catch
        {
            // fall through
        }
        return null;
    }
}
