using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class LoadBalancerEndpoints
{
    public static void MapLoadBalancerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/load-balancers").RequireAuthorization();

        group.MapGet("/", async (LoadBalancerService svc) => Results.Ok(await svc.ListAsync()));

        group.MapGet("/{id:int}", async (int id, LoadBalancerService svc) =>
            await svc.GetAsync(id) is { } l ? Results.Ok(l) : Results.NotFound());

        group.MapPost("/", async (LoadBalancerRequest req, LoadBalancerService svc) =>
        {
            var created = await svc.CreateAsync(req);
            return Results.Created($"/api/load-balancers/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, LoadBalancerRequest req, LoadBalancerService svc) =>
            await svc.UpdateAsync(id, req) is { } l ? Results.Ok(l) : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, LoadBalancerService svc) =>
            await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        group.MapPost("/{id:int}/deploy", async (int id, LoadBalancerService svc) =>
            Results.Ok(await svc.DeployAsync(id)));

        group.MapPost("/{id:int}/start", async (int id, LoadBalancerService svc) =>
            await svc.StartAsync(id) is { } l ? Results.Ok(l) : Results.NotFound());

        group.MapPost("/{id:int}/stop", async (int id, LoadBalancerService svc) =>
            await svc.StopAsync(id) is { } l ? Results.Ok(l) : Results.NotFound());

        group.MapPost("/{id:int}/restart", async (int id, LoadBalancerService svc) =>
            await svc.RestartAsync(id) is { } l ? Results.Ok(l) : Results.NotFound());

        // Fetches a URL through the balanced SOCKS5 listener.
        group.MapPost("/{id:int}/test", async (int id, ProxyTestRequest req, LoadBalancerService svc) =>
            Results.Ok(await svc.TestAsync(id, req.Url)));

        group.MapGet("/{id:int}/logs", async (int id, int? tail, LoadBalancerService svc) =>
            await svc.GetLogsAsync(id, tail ?? 200) is { } logs
                ? Results.Text(logs, "text/plain")
                : Results.NotFound());
    }
}
