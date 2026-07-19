using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;
using GluetunWeb.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

public class ValidationException(string message) : Exception(message);

public class SettingsService(AppDbContext db, ISecretProtector protector, IContainerOrchestrator orchestrator)
{
    // Defaults used when creating the row and when healing blank values left by older migrations.
    public const string DefaultGluetunImage = "qmcgaw/gluetun:latest";
    public const string DefaultSocks5Image = "serjs/go-socks5-proxy";
    public const string DefaultSocks5BalancerImage = "ruepp/socks5balancerasio:latest";

    /// <summary>Returns the singleton settings row, creating defaults on first access.</summary>
    public async Task<GlobalSettings> GetOrCreateAsync(CancellationToken ct = default)
    {
        var settings = await db.GlobalSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new GlobalSettings { Id = 1 };
            db.GlobalSettings.Add(settings);
            await db.SaveChangesAsync(ct);
            return settings;
        }

        // Heal rows created before newer columns existed — migrations backfill non-nullable
        // string columns with "" (e.g. Socks5BalancerImage), which would break image pulls.
        var changed = false;
        if (string.IsNullOrWhiteSpace(settings.GluetunImage)) { settings.GluetunImage = DefaultGluetunImage; changed = true; }
        if (string.IsNullOrWhiteSpace(settings.Socks5Image)) { settings.Socks5Image = DefaultSocks5Image; changed = true; }
        if (string.IsNullOrWhiteSpace(settings.Socks5BalancerImage)) { settings.Socks5BalancerImage = DefaultSocks5BalancerImage; changed = true; }
        if (string.IsNullOrWhiteSpace(settings.PublicIpApi)) { settings.PublicIpApi = "ipinfo"; changed = true; }
        if (string.IsNullOrWhiteSpace(settings.HttpProxyListeningAddress)) { settings.HttpProxyListeningAddress = ":8888"; changed = true; }
        if (settings.PortRangeStart <= 0) { settings.PortRangeStart = 20000; changed = true; }
        if (settings.PortRangeEnd <= 0) { settings.PortRangeEnd = 21000; changed = true; }
        if (settings.BalancerPortRangeStart <= 0) { settings.BalancerPortRangeStart = 30000; changed = true; }
        if (settings.BalancerPortRangeEnd <= 0) { settings.BalancerPortRangeEnd = 31000; changed = true; }
        if (changed) await db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<SettingsResponse> GetResponseAsync(CancellationToken ct = default)
    {
        var s = await GetOrCreateAsync(ct);
        var connected = await orchestrator.PingAsync(ct);
        var endpoint = await orchestrator.GetEndpointAsync(ct);
        return ToResponse(s, connected, endpoint);
    }

    public async Task<SettingsResponse> UpdateAsync(SettingsUpdateRequest r, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ControlServerAuth>(r.ControlServerAuth, ignoreCase: true, out var auth))
            throw new ValidationException($"Unknown control server auth mode '{r.ControlServerAuth}'.");
        if (string.IsNullOrWhiteSpace(r.Timezone))
            throw new ValidationException("Timezone is required.");
        if (string.IsNullOrWhiteSpace(r.GluetunImage) || string.IsNullOrWhiteSpace(r.Socks5Image)
            || string.IsNullOrWhiteSpace(r.Socks5BalancerImage))
            throw new ValidationException("Gluetun, SOCKS5, and balancer image names are required.");
        ValidatePortRange(r.PortRangeStart, r.PortRangeEnd, "Connection");
        ValidatePortRange(r.BalancerPortRangeStart, r.BalancerPortRangeEnd, "Load balancer");

        var s = await GetOrCreateAsync(ct);
        s.Timezone = r.Timezone.Trim();
        s.PublicIpApi = string.IsNullOrWhiteSpace(r.PublicIpApi) ? "ipinfo" : r.PublicIpApi.Trim();
        s.PublicIpApiTokenEnc = ApplySecret(r.PublicIpApiToken, s.PublicIpApiTokenEnc);

        s.HttpProxyEnabled = r.HttpProxyEnabled;
        s.HttpProxyUser = Blank(r.HttpProxyUser);
        s.HttpProxyPasswordEnc = ApplySecret(r.HttpProxyPassword, s.HttpProxyPasswordEnc);
        s.HttpProxyListeningAddress = string.IsNullOrWhiteSpace(r.HttpProxyListeningAddress) ? ":8888" : r.HttpProxyListeningAddress.Trim();
        s.HttpProxyStealth = r.HttpProxyStealth;
        s.HttpProxyLog = r.HttpProxyLog;

        s.ControlServerAuth = auth;
        s.ControlServerUser = Blank(r.ControlServerUser);
        s.ControlServerPasswordEnc = ApplySecret(r.ControlServerPassword, s.ControlServerPasswordEnc);
        s.ControlServerApiKeyEnc = ApplySecret(r.ControlServerApiKey, s.ControlServerApiKeyEnc);

        s.DockerHost = Blank(r.DockerHost);
        s.GluetunImage = r.GluetunImage.Trim();
        s.Socks5Image = r.Socks5Image.Trim();
        s.Socks5BalancerImage = r.Socks5BalancerImage.Trim();
        s.PortRangeStart = r.PortRangeStart;
        s.PortRangeEnd = r.PortRangeEnd;
        s.BalancerPortRangeStart = r.BalancerPortRangeStart;
        s.BalancerPortRangeEnd = r.BalancerPortRangeEnd;
        s.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        var connected = await orchestrator.PingAsync(ct);
        var endpoint = await orchestrator.GetEndpointAsync(ct);
        return ToResponse(s, connected, endpoint);
    }

    private static void ValidatePortRange(int start, int end, string label)
    {
        if (start < 1024 || end > 65535 || start > end)
            throw new ValidationException($"{label} port range must satisfy 1024 <= start <= end <= 65535.");
        if (end - start < 2)
            throw new ValidationException($"{label} port range must span at least 3 ports.");
    }

    private string? ApplySecret(string? incoming, string? existingEnc)
    {
        if (incoming is null) return existingEnc;      // unchanged
        if (incoming.Length == 0) return null;          // cleared
        return protector.Encrypt(incoming);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static SettingsResponse ToResponse(GlobalSettings s, bool connected, string endpoint) => new(
        s.Timezone,
        s.PublicIpApi,
        !string.IsNullOrEmpty(s.PublicIpApiTokenEnc),
        s.HttpProxyEnabled,
        s.HttpProxyUser,
        !string.IsNullOrEmpty(s.HttpProxyPasswordEnc),
        s.HttpProxyListeningAddress,
        s.HttpProxyStealth,
        s.HttpProxyLog,
        s.ControlServerAuth.ToString().ToLowerInvariant(),
        s.ControlServerUser,
        !string.IsNullOrEmpty(s.ControlServerPasswordEnc),
        !string.IsNullOrEmpty(s.ControlServerApiKeyEnc),
        s.DockerHost,
        s.GluetunImage,
        s.Socks5Image,
        s.Socks5BalancerImage,
        s.PortRangeStart,
        s.PortRangeEnd,
        s.BalancerPortRangeStart,
        s.BalancerPortRangeEnd,
        connected,
        endpoint);
}
