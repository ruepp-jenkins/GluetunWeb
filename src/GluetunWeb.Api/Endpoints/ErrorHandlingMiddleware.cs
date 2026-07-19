using GluetunWeb.Api.Models;
using GluetunWeb.Api.Services;

namespace GluetunWeb.Api.Endpoints;

/// <summary>Translates domain exceptions into clean JSON error responses for the SPA.</summary>
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException ex)
        {
            await WriteAsync(ctx, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (PortAllocationException ex)
        {
            await WriteAsync(ctx, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(ctx, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred. Check the server logs.");
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int status, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ApiError(message));
    }
}
