using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections").RequireAuthorization();

        group.MapGet("/", async (ConnectionService svc) => Results.Ok(await svc.ListAsync()));

        group.MapGet("/{id:int}", async (int id, ConnectionService svc) =>
            await svc.GetAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        group.MapPost("/", async (ConnectionRequest req, ConnectionService svc) =>
        {
            var created = await svc.CreateAsync(req);
            return Results.Created($"/api/connections/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, ConnectionRequest req, ConnectionService svc) =>
            await svc.UpdateAsync(id, req) is { } c ? Results.Ok(c) : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, ConnectionService svc) =>
            await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        // Lifecycle actions (real Docker container operations).
        group.MapPost("/{id:int}/deploy", async (int id, ConnectionService svc) =>
            Results.Ok(await svc.DeployAsync(id)));

        group.MapPost("/{id:int}/start", async (int id, ConnectionService svc) =>
            await svc.StartAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        group.MapPost("/{id:int}/stop", async (int id, ConnectionService svc) =>
            await svc.StopAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        group.MapPost("/{id:int}/restart", async (int id, ConnectionService svc) =>
            await svc.RestartAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        // Fetches a URL through this connection's SOCKS5 proxy to prove the tunnel works end to end.
        group.MapPost("/{id:int}/test", async (int id, ProxyTestRequest req, ConnectionService svc) =>
            Results.Ok(await svc.TestAsync(id, req.Url)));

        group.MapGet("/{id:int}/logs", async (int id, int? tail, ConnectionService svc) =>
            await svc.GetLogsAsync(id, tail ?? 200) is { } logs
                ? Results.Text(logs, "text/plain")
                : Results.NotFound());
    }
}
