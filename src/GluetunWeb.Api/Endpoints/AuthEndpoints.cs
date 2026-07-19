using System.Security.Claims;
using GluetunWeb.Api.Auth;
using GluetunWeb.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GluetunWeb.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapGet("/status", async (HttpContext ctx, AuthService auth) =>
        {
            var needsSetup = !await auth.AdminExistsAsync();
            var authed = ctx.User.Identity?.IsAuthenticated == true;
            return Results.Ok(new AuthStatusResponse(needsSetup, authed, authed ? ctx.User.Identity!.Name : null));
        });

        // First-run setup: allowed only while no admin exists. Signs the user in on success.
        group.MapPost("/setup", async (HttpContext ctx, SetupRequest req, AuthService auth) =>
        {
            var (ok, error) = await auth.SetupAsync(req.Username, req.Password);
            if (!ok) return Results.BadRequest(new ApiError(error!));
            await SignInAsync(ctx, req.Username.Trim());
            return Results.Ok(new AuthStatusResponse(false, true, req.Username.Trim()));
        });

        group.MapPost("/login", async (HttpContext ctx, LoginRequest req, AuthService auth) =>
        {
            var user = await auth.ValidateCredentialsAsync(req.Username, req.Password);
            if (user is null) return Results.BadRequest(new ApiError("Invalid username or password."));
            await SignInAsync(ctx, user.Username);
            return Results.Ok(new AuthStatusResponse(false, true, user.Username));
        });

        group.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        }).RequireAuthorization();

        group.MapPost("/change-password", async (HttpContext ctx, ChangePasswordRequest req, AuthService auth) =>
        {
            var username = ctx.User.Identity!.Name!;
            var (ok, error) = await auth.ChangePasswordAsync(username, req.CurrentPassword, req.NewPassword);
            return ok ? Results.Ok() : Results.BadRequest(new ApiError(error!));
        }).RequireAuthorization();
    }

    private static Task SignInAsync(HttpContext ctx, string username)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, username) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        return ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
