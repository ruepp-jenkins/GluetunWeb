using Docker.DotNet;
using Docker.DotNet.Models;

namespace GluetunWeb.Api.Docker;

public class ContainerOrchestrator(DockerClientFactory factory, ILogger<ContainerOrchestrator> logger)
    : IContainerOrchestrator
{
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = await factory.CreateAsync(ct);
            await client.System.PingAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Docker ping failed");
            return false;
        }
    }

    public Task<string> GetEndpointAsync(CancellationToken ct = default) => factory.ResolveEndpointAsync(ct);

    public async Task EnsureImageAsync(string image, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(image))
            throw new ArgumentException("Container image name is not set. Check Global Settings → images.", nameof(image));

        using var client = await factory.CreateAsync(ct);
        try
        {
            await client.Images.InspectImageAsync(image, ct);
            return; // already present
        }
        catch (DockerImageNotFoundException)
        {
            // fall through to pull
        }

        var (fromImage, tag) = ParseImage(image);
        logger.LogInformation("Pulling image {Image}:{Tag}", fromImage, tag);
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
            authConfig: null,
            progress: new Progress<JSONMessage>(),
            cancellationToken: ct);
    }

    public async Task<string> CreateAsync(ContainerSpec spec, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);

        var sharesNetns = spec.NetworkMode?.StartsWith("container:", StringComparison.Ordinal) == true;

        var createParams = new CreateContainerParameters
        {
            Image = spec.Image,
            Name = spec.Name,
            Env = spec.Env.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            Labels = spec.Labels,
            Cmd = spec.Command,
            HostConfig = new HostConfig
            {
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                NetworkMode = spec.NetworkMode,
                ExtraHosts = spec.ExtraHosts.Count > 0 ? spec.ExtraHosts : null,
                // Named volumes keep injected config alive across container recreation.
                Mounts = spec.Volumes.Count > 0
                    ? spec.Volumes.Select(v => new Mount
                    {
                        Type = "volume",
                        Source = v.VolumeName,
                        Target = v.ContainerPath,
                    }).ToList()
                    : null,
            },
        };

        if (spec.NeedTun)
        {
            createParams.HostConfig.CapAdd = new List<string> { "NET_ADMIN" };
            createParams.HostConfig.Devices = new List<DeviceMapping>
            {
                new() { PathOnHost = "/dev/net/tun", PathInContainer = "/dev/net/tun", CgroupPermissions = "rwm" },
            };
        }

        // Ports can only be published on a container that owns its own network namespace.
        // A sidecar using network_mode: container:<id> inherits the parent's published ports.
        if (!sharesNetns && spec.Ports.Count > 0)
        {
            createParams.ExposedPorts = new Dictionary<string, EmptyStruct>();
            createParams.HostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>();
            foreach (var p in spec.Ports)
            {
                var key = $"{p.ContainerPort}/{p.Protocol}";
                createParams.ExposedPorts[key] = default;
                createParams.HostConfig.PortBindings[key] = new List<PortBinding>
                {
                    new() { HostPort = p.HostPort.ToString() },
                };
            }
        }

        var resp = await client.Containers.CreateContainerAsync(createParams, ct);

        // Copy config files in before start, grouped by target directory. Each directory is an
        // existing mount point (a named volume), so the content persists across recreation.
        foreach (var group in spec.Files.GroupBy(f => GetDirectory(f.ContainerPath)))
        {
            using var tar = TarBuilder.BuildFlat(
                group.Select(f => (Path.GetFileName(f.ContainerPath), f.Content)));
            await client.Containers.ExtractArchiveToContainerAsync(
                resp.ID,
                new ContainerPathStatParameters { Path = group.Key, AllowOverwriteDirWithFile = false },
                tar,
                ct);
        }

        return resp.ID;
    }

    public async Task StartAsync(string id, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        await client.Containers.StartContainerAsync(id, new ContainerStartParameters(), ct);
    }

    public async Task StopAsync(string id, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        await client.Containers.StopContainerAsync(id, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct);
    }

    public async Task RemoveAsync(string id, bool force = true, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        try
        {
            await client.Containers.RemoveContainerAsync(id,
                new ContainerRemoveParameters { Force = force, RemoveVolumes = false }, ct);
        }
        catch (DockerContainerNotFoundException)
        {
            // Already gone — treat as success.
        }
    }

    public async Task RemoveByNameAsync(string name, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        var containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true }, ct);

        var target = "/" + name;
        foreach (var c in containers)
        {
            if (c.Names is not null && c.Names.Contains(target, StringComparer.Ordinal))
            {
                try
                {
                    await client.Containers.RemoveContainerAsync(c.ID,
                        new ContainerRemoveParameters { Force = true, RemoveVolumes = false }, ct);
                }
                catch (DockerContainerNotFoundException) { /* raced */ }
            }
        }
    }

    public async Task<ContainerRuntimeInfo?> InspectAsync(string id, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        try
        {
            var r = await client.Containers.InspectContainerAsync(id, ct);
            DateTimeOffset? startedAt = DateTimeOffset.TryParse(r.State.StartedAt, out var s) && s.Year > 1
                ? s
                : null;
            return new ContainerRuntimeInfo(
                r.ID,
                r.State.Status ?? "unknown",
                r.State.Health?.Status,
                (int)r.State.ExitCode,
                startedAt,
                r.HostConfig?.NetworkMode,
                FirstIpAddress(r.NetworkSettings));
        }
        catch (DockerContainerNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// The container's own IP, preferring the per-network entries (the top-level IPAddress is empty
    /// for anything but the default bridge). Null for containers sharing another's namespace.
    /// </summary>
    private static string? FirstIpAddress(NetworkSettings? settings)
    {
        if (settings is null) return null;

        if (settings.Networks is not null)
        {
            foreach (var net in settings.Networks.Values)
            {
                if (!string.IsNullOrWhiteSpace(net?.IPAddress))
                    return net.IPAddress;
            }
        }

        return string.IsNullOrWhiteSpace(settings.IPAddress) ? null : settings.IPAddress;
    }

    public async Task<string> GetLogsAsync(string id, int tail = 200, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        var parameters = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = tail.ToString(),
            Timestamps = false,
        };
        using var stream = await client.Containers.GetContainerLogsAsync(id, tty: false, parameters, ct);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(ct);
        return string.Concat(stdout, stderr);
    }

    public async Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        var containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true }, ct);

        var used = new HashSet<int>();
        foreach (var c in containers)
        {
            if (c.Ports is null) continue;
            foreach (var port in c.Ports)
            {
                if (port.PublicPort != 0)
                    used.Add(port.PublicPort);
            }
        }
        return used;
    }

    public async Task<IReadOnlyList<ManagedContainer>> ListManagedAsync(CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [DockerLabels.ManagedFilter] = true },
            },
        }, ct);

        return containers.Select(c =>
        {
            var name = c.Names?.FirstOrDefault()?.TrimStart('/') ?? c.ID[..Math.Min(12, c.ID.Length)];
            c.Labels.TryGetValue(DockerLabels.ConnectionKey, out var connection);
            c.Labels.TryGetValue(DockerLabels.LoadBalancerKey, out var loadBalancer);
            return new ManagedContainer(c.ID, name, c.Image, c.State ?? "unknown", connection, loadBalancer);
        }).ToList();
    }

    public async Task EnsureVolumeAsync(string name, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        // Idempotent: creating an existing volume returns the existing one.
        await client.Volumes.CreateAsync(new VolumesCreateParameters
        {
            Name = name,
            Labels = new Dictionary<string, string> { [DockerLabels.ManagedByKey] = DockerLabels.ManagedByValue },
        }, ct);
    }

    public async Task RemoveVolumeAsync(string name, CancellationToken ct = default)
    {
        using var client = await factory.CreateAsync(ct);
        try
        {
            await client.Volumes.RemoveAsync(name, force: true, ct);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }

    /// <summary>Container-path directory (always POSIX-style, regardless of host OS).</summary>
    internal static string GetDirectory(string containerPath)
    {
        var idx = containerPath.LastIndexOf('/');
        return idx <= 0 ? "/" : containerPath[..idx];
    }

    internal static (string fromImage, string tag) ParseImage(string image)
    {
        var lastSlash = image.LastIndexOf('/');
        var lastColon = image.LastIndexOf(':');
        if (lastColon > lastSlash)
            return (image[..lastColon], image[(lastColon + 1)..]);
        return (image, "latest");
    }
}
