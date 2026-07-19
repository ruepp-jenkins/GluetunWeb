using GluetunWeb.Api.Auth;
using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;
using GluetunWeb.Api.Endpoints;
using GluetunWeb.Api.Gluetun;
using GluetunWeb.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence (SQLite) ---
var dbPath = Environment.GetEnvironmentVariable("GLUETUNWEB_DB_PATH") ?? "gluetunweb.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// --- Secret protection (AES-256-GCM, key from env) ---
var masterKey = Environment.GetEnvironmentVariable("GLUETUNWEB_MASTER_KEY");
if (string.IsNullOrWhiteSpace(masterKey))
{
    masterKey = "gluetunweb-dev-insecure-key-change-me";
    Console.Error.WriteLine(
        "[WARN] GLUETUNWEB_MASTER_KEY is not set — using an INSECURE development key. " +
        "Set it in production or stored secrets cannot be trusted.");
}
builder.Services.AddSingleton<ISecretProtector>(SecretProtector.FromPassphrase(masterKey));

// --- Docker orchestration ---
builder.Services.AddSingleton<DockerClientFactory>();
builder.Services.AddSingleton<IContainerOrchestrator, ContainerOrchestrator>();

// --- Gluetun control server + proxy connectivity tests ---
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GluetunControlClient>();
builder.Services.AddSingleton<ProxyTester>();
builder.Services.AddSingleton<HostEndpointResolver>();

// --- Application services ---
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<CredentialService>();
builder.Services.AddScoped<ProviderService>();
builder.Services.AddSingleton<ProviderCatalogService>();
// Serializes container lifecycle work against the background reconciler.
builder.Services.AddSingleton<ContainerOperationLock>();
builder.Services.AddHostedService<ProviderCatalogRefreshService>();
builder.Services.AddHostedService<ContainerReconcilerService>();
builder.Services.AddScoped<CustomVpnService>();
builder.Services.AddScoped<PortManager>();
builder.Services.AddScoped<ConnectionService>();
builder.Services.AddScoped<LoadBalancerService>();

// --- Authentication: HttpOnly cookie session ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "gluetunweb.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        // Return status codes for API calls instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// --- Apply migrations + ensure the singleton settings row exists ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await scope.ServiceProvider.GetRequiredService<SettingsService>().GetOrCreateAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

// Serve the built React SPA (wwwroot) with client-side routing fallback.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapSettingsEndpoints();
app.MapCredentialEndpoints();
app.MapProviderEndpoints();
app.MapCustomVpnEndpoints();
app.MapConnectionEndpoints();
app.MapLoadBalancerEndpoints();
app.MapContainerEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// SPA fallback: any non-API, non-file route returns index.html.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
