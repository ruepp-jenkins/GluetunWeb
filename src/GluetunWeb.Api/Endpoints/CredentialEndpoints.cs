using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class CredentialEndpoints
{
    public static void MapCredentialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/credentials").RequireAuthorization();

        group.MapGet("/", async (CredentialService svc) => Results.Ok(await svc.ListAsync()));

        group.MapGet("/{id:int}", async (int id, CredentialService svc) =>
            await svc.GetAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        // Connections that carry this credential's secrets — used to offer a redeploy after an edit,
        // since a running container keeps the old values until it is recreated.
        group.MapGet("/{id:int}/connections", async (int id, CredentialService svc) =>
            Results.Ok(await svc.GetAffectedConnectionsAsync(id)));

        group.MapPost("/", async (CredentialRequest req, CredentialService svc) =>
        {
            var created = await svc.CreateAsync(req);
            return Results.Created($"/api/credentials/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, CredentialRequest req, CredentialService svc) =>
            await svc.UpdateAsync(id, req) is { } c ? Results.Ok(c) : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, CredentialService svc) =>
            await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
    }
}
