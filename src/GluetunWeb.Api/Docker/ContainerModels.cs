namespace GluetunWeb.Api.Docker;

/// <summary>Docker labels stamped on every container GluetunWeb creates, used to detect ownership.</summary>
public static class DockerLabels
{
    public const string ManagedByKey = "managed-by";
    public const string ManagedByValue = "gluetunweb";
    public const string ConnectionKey = "gluetunweb.connection";
    public const string LoadBalancerKey = "gluetunweb.loadbalancer";
    /// <summary>Docker list filter value, e.g. label=managed-by=gluetunweb.</summary>
    public const string ManagedFilter = ManagedByKey + "=" + ManagedByValue;
}

/// <summary>A host&lt;-&gt;container port publish (host port is chosen by the port manager).</summary>
public record PortMap(int HostPort, int ContainerPort, string Protocol = "tcp");

/// <summary>A file to inject into a container (via the Docker copy API) before it starts.</summary>
public record FileToCopy(string ContainerPath, string Content);

/// <summary>
/// A named Docker volume mounted into the container. Injected config lives here so it survives
/// container recreation (e.g. a Watchtower image update), which discards the writable layer.
/// </summary>
public record VolumeMount(string VolumeName, string ContainerPath);

/// <summary>Declarative description of a container to create. Consumed by the orchestrator.</summary>
public class ContainerSpec
{
    public required string Image { get; init; }
    public required string Name { get; init; }
    public Dictionary<string, string> Env { get; } = new();
    public List<PortMap> Ports { get; } = new();
    /// <summary>Adds NET_ADMIN + /dev/net/tun (required by the Gluetun container).</summary>
    public bool NeedTun { get; init; }
    /// <summary>e.g. "container:&lt;gluetunId&gt;" to share a network namespace (SOCKS5 sidecar).</summary>
    public string? NetworkMode { get; init; }
    /// <summary>Files copied into the container before it starts (config.toml, custom .ovpn, …).</summary>
    public List<FileToCopy> Files { get; } = new();
    /// <summary>Named volumes to mount (config files are written into these so they persist).</summary>
    public List<VolumeMount> Volumes { get; } = new();
    /// <summary>Optional command override (e.g. passing a config path to the balancer binary).</summary>
    public IList<string>? Command { get; init; }
    public Dictionary<string, string> Labels { get; } = new();
    /// <summary>Extra /etc/hosts entries, e.g. "host.docker.internal:host-gateway" for the balancer.</summary>
    public List<string> ExtraHosts { get; } = new();
}

/// <summary>A container carrying GluetunWeb's management label, as discovered from Docker.</summary>
public record ManagedContainer(
    string Id, string Name, string Image, string State, string? ConnectionLabel, string? LoadBalancerLabel);

/// <summary>Live runtime state read back from Docker for a container.</summary>
public record ContainerRuntimeInfo(
    string Id,
    string State,       // running | exited | created | restarting | ...
    string? Health,     // healthy | unhealthy | starting | null
    int? ExitCode,
    DateTimeOffset? StartedAt,
    /// <summary>e.g. "container:&lt;id&gt;" — used to detect a stale sidecar netns link.</summary>
    string? NetworkMode = null,
    /// <summary>
    /// Bridge-network IP of the container, when it owns a namespace. Used to reach Gluetun's control
    /// server and the proxies directly, which works both from the host (Linux) and from a sibling
    /// container — unlike localhost, which only works when the API runs outside Docker.
    /// </summary>
    string? IpAddress = null);
