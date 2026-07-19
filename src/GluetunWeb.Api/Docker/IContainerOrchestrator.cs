namespace GluetunWeb.Api.Docker;

/// <summary>
/// Abstraction over the Docker Engine API for the full container lifecycle. Keeping this behind an
/// interface isolates Docker.DotNet from the service layer and makes the services unit-testable.
/// </summary>
public interface IContainerOrchestrator
{
    /// <summary>True if the Docker/Podman endpoint is reachable.</summary>
    Task<bool> PingAsync(CancellationToken ct = default);

    /// <summary>The resolved endpoint string (socket or TCP) for display in the UI.</summary>
    Task<string> GetEndpointAsync(CancellationToken ct = default);

    /// <summary>Pulls the image if it is not already present locally.</summary>
    Task EnsureImageAsync(string image, CancellationToken ct = default);

    /// <summary>
    /// Creates a container (and copies any spec files into it) but does NOT start it.
    /// Returns the new container id.
    /// </summary>
    Task<string> CreateAsync(ContainerSpec spec, CancellationToken ct = default);

    Task StartAsync(string id, CancellationToken ct = default);
    Task StopAsync(string id, CancellationToken ct = default);
    Task RemoveAsync(string id, bool force = true, CancellationToken ct = default);

    /// <summary>Removes any container whose exact name matches (handles orphans on redeploy).</summary>
    Task RemoveByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Returns runtime info, or null if the container no longer exists.</summary>
    Task<ContainerRuntimeInfo?> InspectAsync(string id, CancellationToken ct = default);

    /// <summary>Returns the last <paramref name="tail"/> lines of combined stdout/stderr.</summary>
    Task<string> GetLogsAsync(string id, int tail = 200, CancellationToken ct = default);

    /// <summary>All host ports currently published by containers (for collision avoidance).</summary>
    Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken ct = default);

    /// <summary>All containers carrying GluetunWeb's management label (running or stopped).</summary>
    Task<IReadOnlyList<ManagedContainer>> ListManagedAsync(CancellationToken ct = default);

    /// <summary>Creates the named volume if it does not exist (idempotent).</summary>
    Task EnsureVolumeAsync(string name, CancellationToken ct = default);

    /// <summary>Removes a named volume (ignores "not found").</summary>
    Task RemoveVolumeAsync(string name, CancellationToken ct = default);
}
