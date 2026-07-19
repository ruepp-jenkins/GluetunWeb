using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GluetunWeb.Api.Data;

namespace GluetunWeb.Api.Gluetun;

/// <summary>What Gluetun itself reports about a connection, as opposed to what Docker reports.</summary>
public record GluetunVpnState(
    string? VpnStatus,       // running | stopped | crashed | null when unreachable
    string? PublicIp,
    string? Country,
    string? City,
    int? ForwardedPort,
    string? Error);

/// <summary>
/// Reads Gluetun's HTTP control server. This is the difference between "the container is running"
/// and "the tunnel is up" — Gluetun retries internally rather than exiting, so Docker reports
/// <c>running</c> for a connection that has never connected.
///
/// Results are cached briefly because the connections list polls every few seconds and each refresh
/// would otherwise issue three HTTP calls per connection.
/// </summary>
public class GluetunControlClient(IHttpClientFactory httpClientFactory, ILogger<GluetunControlClient> logger)
{
    /// <summary>Gluetun's control server port inside the container.</summary>
    public const int ControlPort = 8000;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, GluetunVpnState State)> _cache = new();

    /// <summary>
    /// Queries status, public IP and forwarded port. <paramref name="baseUrl"/> should target the
    /// container's own IP where possible; the published host port only works when the API runs
    /// outside Docker.
    /// </summary>
    public async Task<GluetunVpnState> GetStateAsync(
        string cacheKey, string baseUrl, GlobalSettings settings, string? user, string? password,
        string? apiKey, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cacheKey, out var hit) && DateTimeOffset.UtcNow - hit.At < CacheTtl)
            return hit.State;

        var state = await FetchAsync(baseUrl, settings, user, password, apiKey, ct);
        _cache[cacheKey] = (DateTimeOffset.UtcNow, state);
        return state;
    }

    /// <summary>Drops the cached state so the next read reflects a just-completed action.</summary>
    public void Invalidate(string cacheKey) => _cache.TryRemove(cacheKey, out _);

    private async Task<GluetunVpnState> FetchAsync(
        string baseUrl, GlobalSettings settings, string? user, string? password, string? apiKey,
        CancellationToken ct)
    {
        using var http = httpClientFactory.CreateClient("gluetun-control");
        http.Timeout = Timeout;
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        ApplyAuth(http, settings.ControlServerAuth, user, password, apiKey);

        try
        {
            var status = await GetStringPropertyAsync(http, "v1/vpn/status", "status", ct);

            string? ip = null, country = null, city = null;
            var publicIp = await GetJsonAsync(http, "v1/publicip/ip", ct);
            if (publicIp is not null)
            {
                ip = ReadString(publicIp.Value, "public_ip") ?? ReadString(publicIp.Value, "ip");
                country = ReadString(publicIp.Value, "country");
                city = ReadString(publicIp.Value, "city");
            }

            // Only present when port forwarding is enabled; a 404/403 here is not an error.
            int? forwarded = null;
            var pf = await GetJsonAsync(http, "v1/portforward", ct)
                     ?? await GetJsonAsync(http, "v1/openvpn/portforwarded", ct);
            if (pf is not null && pf.Value.TryGetProperty("port", out var portEl)
                && portEl.ValueKind == JsonValueKind.Number && portEl.TryGetInt32(out var p) && p > 0)
            {
                forwarded = p;
            }

            return new GluetunVpnState(status, ip, country, city, forwarded, null);
        }
        catch (Exception ex)
        {
            // Unreachable control server is normal (container starting, stopped, auth misconfigured)
            // — surfaced as an error string rather than thrown, so the list still renders.
            logger.LogDebug(ex, "Gluetun control server unreachable at {BaseUrl}", baseUrl);
            return new GluetunVpnState(null, null, null, null, null, Describe(ex));
        }
    }

    private static void ApplyAuth(
        HttpClient http, ControlServerAuth auth, string? user, string? password, string? apiKey)
    {
        switch (auth)
        {
            case ControlServerAuth.Basic when !string.IsNullOrEmpty(user):
                var raw = Encoding.UTF8.GetBytes($"{user}:{password}");
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
                break;
            case ControlServerAuth.ApiKey when !string.IsNullOrEmpty(apiKey):
                http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                break;
        }
    }

    private static async Task<string?> GetStringPropertyAsync(
        HttpClient http, string path, string property, CancellationToken ct)
    {
        var json = await GetJsonAsync(http, path, ct);
        return json is null ? null : ReadString(json.Value, property);
    }

    private static async Task<JsonElement?> GetJsonAsync(HttpClient http, string path, CancellationToken ct)
    {
        using var resp = await http.GetAsync(path, ct);
        if (!resp.IsSuccessStatusCode)
            return null;
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            // Clone so the value outlives the JsonDocument.
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement obj, string property)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(property, out var v)
           && v.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException => "control server timed out",
        HttpRequestException h => $"control server unreachable ({h.StatusCode?.ToString() ?? "no response"})",
        _ => "control server unreachable",
    };
}
