namespace GluetunWeb.Api.Gluetun;

/// <summary>Values extracted from a WireGuard .conf, mapped to Gluetun custom-provider env vars.</summary>
public record WireGuardParsed(
    string? PrivateKey,
    string? Addresses,
    string? PublicKey,
    string? PresharedKey,
    string? EndpointHost,
    string? EndpointPort,
    string? Dns);

/// <summary>
/// Parses a WireGuard configuration file ([Interface]/[Peer] INI-style) into the fields Gluetun
/// needs when running a custom WireGuard connection (WIREGUARD_* env vars).
/// </summary>
public static class WireGuardConfigParser
{
    public static WireGuardParsed Parse(string config)
    {
        string? privateKey = null, addresses = null, dns = null;
        string? publicKey = null, presharedKey = null, endpointHost = null, endpointPort = null;

        var section = string.Empty;
        foreach (var rawLine in config.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line.Trim('[', ']').Trim().ToLowerInvariant();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim().ToLowerInvariant();
            var value = line[(eq + 1)..].Trim();

            switch (section, key)
            {
                case ("interface", "privatekey"): privateKey = value; break;
                case ("interface", "address"): addresses = value; break;
                case ("interface", "dns"): dns = value; break;
                case ("peer", "publickey"): publicKey = value; break;
                case ("peer", "presharedkey"): presharedKey = value; break;
                case ("peer", "endpoint"):
                    var idx = value.LastIndexOf(':');
                    if (idx > 0)
                    {
                        endpointHost = value[..idx];
                        endpointPort = value[(idx + 1)..];
                    }
                    else
                    {
                        endpointHost = value;
                    }
                    break;
            }
        }

        return new WireGuardParsed(privateKey, addresses, publicKey, presharedKey, endpointHost, endpointPort, dns);
    }

    /// <summary>Returns a validation error message, or null when the config has the required fields.</summary>
    public static string? Validate(string config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return "WireGuard config is empty.";
        var p = Parse(config);
        if (string.IsNullOrWhiteSpace(p.PrivateKey))
            return "WireGuard config is missing [Interface] PrivateKey.";
        if (string.IsNullOrWhiteSpace(p.Addresses))
            return "WireGuard config is missing [Interface] Address.";
        if (string.IsNullOrWhiteSpace(p.PublicKey))
            return "WireGuard config is missing [Peer] PublicKey.";
        if (string.IsNullOrWhiteSpace(p.EndpointHost))
            return "WireGuard config is missing [Peer] Endpoint.";
        return null;
    }
}

/// <summary>Lightweight sanity checks for an OpenVPN .ovpn file.</summary>
public static class OpenVpnConfigValidator
{
    public static string? Validate(string config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return "OpenVPN config is empty.";
        var hasRemote = config.Contains("remote ", StringComparison.OrdinalIgnoreCase);
        var hasClient = config.Contains("client", StringComparison.OrdinalIgnoreCase)
                        || config.Contains("tls-client", StringComparison.OrdinalIgnoreCase);
        if (!hasRemote)
            return "OpenVPN config must contain at least one 'remote' directive.";
        if (!hasClient)
            return "OpenVPN config does not look like a client profile (missing 'client').";
        return null;
    }
}
