using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace GluetunWeb.Api.Services;

public record ProviderCatalog(IReadOnlyList<string> Providers, string Source, DateTimeOffset? UpdatedAt);

/// <summary>One server entry from a provider's servers JSON, reduced to what the UI filters on.</summary>
public record ProviderServerRecord(
    string Vpn, string? Region, string? Country, string? City, string? Hostname, bool Tcp, bool Udp);

/// <summary>
/// Narrows the option lists. Empty arrays mean "no constraint at this level". Selections are
/// multi-value because Gluetun's SERVER_* variables take comma-separated lists.
/// </summary>
public record ProviderServerFilter(
    string? VpnType = null,
    IReadOnlyList<string>? Regions = null,
    IReadOnlyList<string>? Countries = null,
    IReadOnlyList<string>? Cities = null)
{
    public static readonly ProviderServerFilter None = new();
}

/// <summary>
/// Selectable values for a provider, narrowed by the filter. Each level is filtered only by the
/// levels *above* it (region → country → city → hostname), so picking a country narrows cities and
/// hostnames but still lets you switch to a different country.
///
/// Also reports which OpenVPN transports the provider's servers offer: Gluetun filters by
/// OPENVPN_PROTOCOL, so offering a protocol no server supports (e.g. TCP on Privado) fails at
/// connect time with an opaque "no server found".
/// </summary>
public record ProviderServerOptions(
    string Provider,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Hostnames,
    bool HostnamesTruncated = false,
    bool HasOpenVpn = false,
    bool SupportsTcp = false,
    bool SupportsUdp = false);

/// <summary>
/// Provides the list of valid Gluetun provider names by cloning qdm12/gluetun-servers and reading
/// the filenames under pkg/servers (each &lt;provider&gt;.json's name is the exact VPN_SERVICE_PROVIDER
/// value). The clone is persisted (on the data volume) and updated via git pull — on demand and on a
/// periodic schedule (see ProviderCatalogRefreshService). No hardcoded list is kept; when the repo is
/// unreachable the catalog is simply empty and the provider field falls back to free-text entry.
/// </summary>
public class ProviderCatalogService
{
    private readonly ILogger<ProviderCatalogService> _logger;
    private readonly string _repoUrl;
    private readonly string _clonePath;
    private readonly string _serversDir;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProviderCatalog? _cache;
    // Parsed server records per provider (cleared on git refresh). Filtering happens in memory from
    // these, so a cascading dropdown does not re-read or re-parse the JSON on every keystroke.
    private readonly ConcurrentDictionary<string, IReadOnlyList<ProviderServerRecord>> _serverRecords =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cap on returned hostnames. Unfiltered, a large provider has thousands (NordVPN ships ~9k
    /// OpenVPN entries) — far more than a datalist is useful for. Narrowing by country/city brings
    /// it under the cap almost immediately.
    /// </summary>
    public const int MaxHostnames = 500;

    public ProviderCatalogService(ILogger<ProviderCatalogService> logger)
    {
        _logger = logger;
        _repoUrl = Environment.GetEnvironmentVariable("GLUETUNWEB_SERVERS_REPO")
                   ?? "https://github.com/qdm12/gluetun-servers.git";

        var configured = Environment.GetEnvironmentVariable("GLUETUNWEB_SERVERS_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _clonePath = configured;
        }
        else
        {
            var dbPath = Environment.GetEnvironmentVariable("GLUETUNWEB_DB_PATH") ?? "gluetunweb.db";
            var dataDir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".";
            _clonePath = Path.Combine(dataDir, "gluetun-servers");
        }
        _serversDir = Path.Combine(_clonePath, "pkg", "servers");
    }

    public async Task<ProviderCatalog> GetAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _cache is not null)
                return _cache;

            await EnsureRepoAsync(forceRefresh, ct);
            if (forceRefresh)
                _serverRecords.Clear(); // re-parse against the freshly pulled files
            var providers = ReadProviders();
            var catalog = new ProviderCatalog(providers, providers.Count > 0 ? "git" : "unavailable", DateTimeOffset.UtcNow);
            // Only cache a successful (non-empty) result, so a transient failure retries next time.
            if (providers.Count > 0)
                _cache = catalog;
            return catalog;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Clones (first run) or pulls (refresh) the repo. Best-effort — logs on failure.</summary>
    private async Task EnsureRepoAsync(bool forceRefresh, CancellationToken ct)
    {
        var cloned = Directory.Exists(Path.Combine(_clonePath, ".git"));
        try
        {
            if (!cloned)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_clonePath.TrimEnd('/')) ?? ".");
                var (ok, err) = await RunGitAsync(new[] { "clone", "--depth", "1", _repoUrl, _clonePath }, workingDir: ".", ct);
                if (!ok)
                    _logger.LogWarning("git clone of {Repo} failed: {Err}", _repoUrl, err);
                return;
            }

            if (forceRefresh)
            {
                var (fok, ferr) = await RunGitAsync(new[] { "fetch", "--depth", "1", "origin" }, _clonePath, ct);
                if (fok)
                    await RunGitAsync(new[] { "reset", "--hard", "FETCH_HEAD" }, _clonePath, ct);
                else
                    _logger.LogWarning("git fetch failed (using existing checkout): {Err}", ferr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider catalog git operation failed");
        }
    }

    private List<string> ReadProviders()
    {
        if (!Directory.Exists(_serversDir))
            return new List<string>();

        return Directory.EnumerateFiles(_serversDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns the selectable region/country/city/hostname values for a provider, narrowed by
    /// <paramref name="filter"/>. Records are parsed once per provider and cached; filtering runs in
    /// memory. Unknown providers return empty lists so free-text entry still works.
    /// </summary>
    public async Task<ProviderServerOptions> GetServerOptionsAsync(
        string provider, ProviderServerFilter? filter = null, CancellationToken ct = default)
    {
        // Path.GetFileName neutralizes any traversal while preserving spaces in provider names.
        var name = Path.GetFileName((provider ?? string.Empty).Trim());
        if (string.IsNullOrEmpty(name))
            return Unknown(provider ?? string.Empty);

        if (!_serverRecords.TryGetValue(name, out var records))
        {
            records = await ParseServerRecordsAsync(Path.Combine(_serversDir, name + ".json"), ct);
            _serverRecords[name] = records;
        }

        // No data for this provider (unlisted, or the catalog is unavailable) — do not constrain.
        if (records.Count == 0)
            return Unknown(name);

        return BuildOptions(name, records, filter ?? ProviderServerFilter.None);
    }

    /// <summary>
    /// Applies the cascade: every level is filtered by the levels above it only, so a selection
    /// narrows what follows without hiding the alternatives at its own level.
    /// </summary>
    internal static ProviderServerOptions BuildOptions(
        string name, IReadOnlyList<ProviderServerRecord> records, ProviderServerFilter filter)
    {
        // VPN type applies to every level: an OpenVPN connection must not offer WireGuard hostnames.
        var byVpn = records.Where(r => MatchesVpn(r, filter.VpnType)).ToList();

        var byRegion = byVpn.Where(r => Matches(r.Region, filter.Regions)).ToList();
        var byCountry = byRegion.Where(r => Matches(r.Country, filter.Countries)).ToList();
        var byCity = byCountry.Where(r => Matches(r.City, filter.Cities)).ToList();

        var hostnames = Distinct(byCity.Select(r => r.Hostname));
        var truncated = hostnames.Count > MaxHostnames;

        // Transport support reflects the current narrowing, so a country with only UDP servers
        // disables TCP rather than reporting the provider-wide answer.
        var openVpn = byCity.Where(r => string.Equals(r.Vpn, "openvpn", StringComparison.OrdinalIgnoreCase)).ToList();

        return new ProviderServerOptions(
            name,
            Regions: Distinct(byVpn.Select(r => r.Region)),
            Countries: Distinct(byRegion.Select(r => r.Country)),
            Cities: Distinct(byCountry.Select(r => r.City)),
            Hostnames: truncated ? hostnames.Take(MaxHostnames).ToList() : hostnames,
            HostnamesTruncated: truncated,
            HasOpenVpn: openVpn.Count > 0,
            SupportsTcp: openVpn.Any(r => r.Tcp),
            SupportsUdp: openVpn.Any(r => r.Udp));
    }

    private static bool MatchesVpn(ProviderServerRecord r, string? vpnType)
        => string.IsNullOrWhiteSpace(vpnType)
           || string.Equals(r.Vpn, vpnType, StringComparison.OrdinalIgnoreCase);

    /// <summary>An empty selection means "no constraint"; otherwise the value must be one of them.</summary>
    private static bool Matches(string? value, IReadOnlyList<string>? selected)
        => selected is null || selected.Count == 0
           || (value is not null && selected.Contains(value, StringComparer.OrdinalIgnoreCase));

    private static ProviderServerOptions Empty(string name) =>
        new(name, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    /// <summary>
    /// Unknown provider (free-text, or the catalog is unavailable) — allow both protocols rather
    /// than blocking the user on missing data.
    /// </summary>
    private static ProviderServerOptions Unknown(string name) =>
        Empty(name) with { HasOpenVpn = true, SupportsTcp = true, SupportsUdp = true };

    private static async Task<IReadOnlyList<ProviderServerRecord>> ParseServerRecordsAsync(
        string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return Array.Empty<ProviderServerRecord>();
        try
        {
            return ParseServerRecordsJson(await File.ReadAllTextAsync(path, ct));
        }
        catch
        {
            // Malformed/unreadable JSON → no records; the UI falls back to free-text entry.
            return Array.Empty<ProviderServerRecord>();
        }
    }

    /// <summary>Reduces a servers JSON document to the fields the selection UI filters on.</summary>
    internal static IReadOnlyList<ProviderServerRecord> ParseServerRecordsJson(string json)
    {
        var result = new List<ProviderServerRecord>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("servers", out var servers)
            || servers.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var s in servers.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.Object) continue;
            result.Add(new ProviderServerRecord(
                ReadString(s, "vpn") ?? string.Empty,
                ReadString(s, "region"),
                ReadString(s, "country"),
                ReadString(s, "city"),
                ReadString(s, "hostname"),
                IsTrue(s, "tcp"),
                IsTrue(s, "udp")));
        }

        return result;
    }

    private static bool IsTrue(JsonElement obj, string prop) =>
        obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        var s = v.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string?> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<(bool ok, string error)> RunGitAsync(string[] args, string workingDir, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDir,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc is null) return (false, "could not start git");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var stderr = await proc.StandardError.ReadToEndAsync(timeout.Token);
            await proc.WaitForExitAsync(timeout.Token);
            return (proc.ExitCode == 0, stderr);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
