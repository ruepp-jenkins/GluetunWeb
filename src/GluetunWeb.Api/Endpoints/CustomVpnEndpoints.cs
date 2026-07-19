using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class CustomVpnEndpoints
{
    public static void MapCustomVpnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/custom-vpn").RequireAuthorization();

        group.MapGet("/", async (CustomVpnService svc) => Results.Ok(await svc.ListAsync()));

        group.MapGet("/{id:int}", async (int id, CustomVpnService svc) =>
            await svc.GetAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        // Decrypted config text for the authenticated admin to edit (contains keys — auth required).
        group.MapGet("/{id:int}/raw", async (int id, CustomVpnService svc) =>
            await svc.GetRawConfigAsync(id) is { } raw
                ? Results.Ok(new Models.CustomVpnRawResponse(raw))
                : Results.NotFound());

        // Accepts raw config text (the SPA reads uploaded .ovpn/.conf files and posts their contents here).
        group.MapPost("/", async (CustomVpnRequest req, CustomVpnService svc) =>
        {
            var created = await svc.CreateAsync(req);
            return Results.Created($"/api/custom-vpn/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, CustomVpnRequest req, CustomVpnService svc) =>
            await svc.UpdateAsync(id, req) is { } c ? Results.Ok(c) : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, CustomVpnService svc) =>
            await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
    }
}
