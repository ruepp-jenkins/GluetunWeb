using System.Net;
using System.Net.Sockets;

namespace GluetunWeb.Api.Gluetun;

/// <summary>
/// Gluetun rejects domain names for VPN endpoints — it needs a literal IP. Users put the
/// <see cref="Placeholder"/> token where the IP belongs in their custom config and set an endpoint
/// DNS name; at deploy time the name is resolved and the placeholder is substituted.
/// </summary>
public static class EndpointResolver
{
    public const string Placeholder = "{{DNS_IP}}";

    /// <summary>True when the config contains the DNS placeholder token.</summary>
    public static bool HasPlaceholder(string? config)
        => config is not null && config.Contains(Placeholder, StringComparison.Ordinal);

    /// <summary>Pure substitution of the placeholder with a resolved IP (testable).</summary>
    public static string Substitute(string config, string ip)
        => config.Replace(Placeholder, ip, StringComparison.Ordinal);

    /// <summary>
    /// Resolves <paramref name="dnsName"/> and replaces the placeholder in <paramref name="config"/>.
    /// No-op when there is no DNS name or no placeholder. Throws when resolution fails.
    /// </summary>
    public static async Task<string> ResolveAndSubstituteAsync(
        string config, string? dnsName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dnsName) || !HasPlaceholder(config))
            return config;

        var ip = await ResolveIpAsync(dnsName.Trim(), ct);
        return Substitute(config, ip);
    }

    private static async Task<string> ResolveIpAsync(string host, CancellationToken ct)
    {
        // Already an IP? Use as-is.
        if (IPAddress.TryParse(host, out var literal))
            return literal.ToString();

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not resolve endpoint DNS name '{host}': {ex.Message}");
        }

        var chosen = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                     ?? addresses.FirstOrDefault();
        if (chosen is null)
            throw new InvalidOperationException($"Endpoint DNS name '{host}' resolved to no addresses.");

        return chosen.ToString();
    }
}
