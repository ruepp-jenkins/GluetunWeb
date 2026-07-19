using GluetunWeb.Api.Gluetun;
using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();

        group.MapGet("/", async (SettingsService svc) => Results.Ok(await svc.GetResponseAsync()));

        group.MapPut("/", async (SettingsUpdateRequest req, SettingsService svc) =>
            Results.Ok(await svc.UpdateAsync(req)));

        // Helper for the UI to mint a control-server API key (mirrors `gluetun genkey`).
        group.MapPost("/generate-apikey", () => Results.Ok(new ApiKeyResponse(ApiKeyGenerator.Generate())));
    }
}
