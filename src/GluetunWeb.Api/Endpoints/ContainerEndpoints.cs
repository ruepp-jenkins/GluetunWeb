using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class ContainerEndpoints
{
    public static void MapContainerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/containers").RequireAuthorization();

        // All GluetunWeb-labeled containers, each flagged known vs orphaned.
        group.MapGet("/", async (ConnectionService svc) => Results.Ok(await svc.ListManagedContainersAsync()));

        // Remove a labeled container from Docker (label-verified server-side).
        group.MapDelete("/{id}", async (string id, ConnectionService svc) =>
        {
            await svc.RemoveManagedContainerAsync(id);
            return Results.NoContent();
        });
    }
}
