using Docker.DotNet;
using GluetunWeb.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Docker;

/// <summary>
/// Resolves how to reach the Docker (or Podman) Engine API and builds a client.
///
/// Precedence: GlobalSettings.DockerHost (TCP override set in the UI) → DOCKER_HOST env var →
/// the local Unix socket (/var/run/docker.sock). Podman works unchanged by pointing DockerHost at
/// its rootless socket, e.g. unix:///run/user/1000/podman/podman.sock — see docs/PODMAN.md.
/// </summary>
public class DockerClientFactory(IServiceScopeFactory scopeFactory)
{
    public const string DefaultUnixSocket = "unix:///var/run/docker.sock";

    public async Task<IDockerClient> CreateAsync(CancellationToken ct = default)
    {
        var endpoint = await ResolveEndpointAsync(ct);
        return new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
    }

    public async Task<string> ResolveEndpointAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.GlobalSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(settings?.DockerHost))
            return settings.DockerHost.Trim();

        var envHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(envHost))
            return envHost.Trim();

        return DefaultUnixSocket;
    }
}
