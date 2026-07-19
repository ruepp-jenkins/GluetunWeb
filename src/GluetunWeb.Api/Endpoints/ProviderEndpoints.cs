using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/providers").RequireAuthorization();

        group.MapGet("/", async (ProviderService svc) => Results.Ok(await svc.ListAsync()));

        // Valid provider names sourced from qdm12/gluetun-servers (pkg/servers/*.json).
        group.MapGet("/catalog", async (ProviderCatalogService svc) => Results.Ok(await svc.GetAsync()));
        group.MapPost("/catalog/refresh", async (ProviderCatalogService svc) =>
            Results.Ok(await svc.GetAsync(forceRefresh: true)));

        // Selectable region/country/city/hostname values for a provider, narrowed by the levels
        // already chosen (region → country → city → hostname).
        group.MapGet("/catalog/servers", async (
            string provider,
            string? vpnType,
            string? regions,
            string? countries,
            string? cities,
            ProviderCatalogService svc) =>
        {
            var filter = new ProviderServerFilter(
                vpnType, SplitCsv(regions), SplitCsv(countries), SplitCsv(cities));
            return Results.Ok(await svc.GetServerOptionsAsync(provider, filter));
        });

        group.MapGet("/{id:int}", async (int id, ProviderService svc) =>
            await svc.GetAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());

        group.MapPost("/", async (ProviderRequest req, ProviderService svc) =>
        {
            var created = await svc.CreateAsync(req);
            return Results.Created($"/api/providers/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, ProviderRequest req, ProviderService svc) =>
            await svc.UpdateAsync(id, req) is { } p ? Results.Ok(p) : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, ProviderService svc) =>
            await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
    }

    /// <summary>Splits a comma-separated query value; blank entries are dropped.</summary>
    private static string[] SplitCsv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
