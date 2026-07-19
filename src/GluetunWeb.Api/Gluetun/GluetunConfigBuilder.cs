using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;

namespace GluetunWeb.Api.Gluetun;

/// <summary>
/// Fully-resolved (decrypted) inputs needed to materialize a Gluetun container's env + files.
/// Kept free of entities/crypto so it can be unit-tested directly.
/// </summary>
public record GluetunBuildInput
{
    // Global settings
    public string Timezone { get; init; } = "UTC";
    public string PublicIpApi { get; init; } = "ipinfo";
    public string? PublicIpApiToken { get; init; }
    public bool HttpProxyEnabled { get; init; }
    public string? HttpProxyUser { get; init; }
    public string? HttpProxyPassword { get; init; }
    public string HttpProxyListeningAddress { get; init; } = ":8888";
    public bool HttpProxyStealth { get; init; }
    public bool HttpProxyLog { get; init; }

    // Port forwarding + firewall (per connection).
    public bool PortForwarding { get; init; }
    public string? PortForwardingProvider { get; init; }
    public int PortForwardingPortsCount { get; init; } = 1;
    public string? FirewallVpnInputPorts { get; init; }
    public string? FirewallOutboundSubnets { get; init; }
    public int? WireGuardMtu { get; init; }

    // DNS filtering (per connection).
    public bool BlockMalicious { get; init; } = true;
    public bool BlockAds { get; init; }
    public string? DnsUnblockHostnames { get; init; }

    // Gluetun's built-in Shadowsocks server (per connection).
    public bool ShadowsocksEnabled { get; init; }
    public string? ShadowsocksPassword { get; init; }
    public string ShadowsocksCipher { get; init; } = GluetunConfigBuilder.DefaultShadowsocksCipher;
    public bool ShadowsocksLog { get; init; }
    public ControlServerAuth ControlAuth { get; init; } = ControlServerAuth.None;
    public string? ControlUser { get; init; }
    public string? ControlPassword { get; init; }
    public string? ControlApiKey { get; init; }

    // VPN source
    public bool IsCustom { get; init; }
    public VpnType VpnType { get; init; } = VpnType.OpenVpn;

    /// <summary>OPENVPN_PROTOCOL — emitted only on the OpenVPN path (WireGuard is UDP-only).</summary>
    public OpenVpnProtocol OpenVpnProtocol { get; init; } = OpenVpnProtocol.Udp;

    // Provider path
    public string? ProviderType { get; init; }
    public string? OpenVpnUser { get; init; }
    public string? OpenVpnPassword { get; init; }
    public string? WireGuardPrivateKey { get; init; }
    public string? WireGuardPresharedKey { get; init; }
    public string? WireGuardAddresses { get; init; }
    public string? ServerCountries { get; init; }
    public string? ServerCities { get; init; }
    public string? ServerRegions { get; init; }
    public string? ServerHostnames { get; init; }

    // Custom path
    public string? CustomRawConfig { get; init; }
}

public record GluetunBuildResult(Dictionary<string, string> Env, List<FileToCopy> Files);

public static class GluetunConfigBuilder
{
    /// <summary>
    /// Injected config lives on a per-connection named volume mounted here (NOT under /gluetun,
    /// which would shadow the image's own contents) so it survives container recreation.
    /// </summary>
    public const string ConfigVolumePath = "/gluetunweb";
    public const string ControlConfigPath = ConfigVolumePath + "/config.toml";
    public const string CustomOpenVpnPath = ConfigVolumePath + "/custom.ovpn";

    /// <summary>Gluetun's default Shadowsocks port (SHADOWSOCKS_LISTENING_ADDRESS), TCP and UDP.</summary>
    public const int ShadowsocksPort = 8388;
    public const string DefaultShadowsocksCipher = "chacha20-ietf-poly1305";

    /// <summary>The AEAD ciphers Gluetun accepts for SHADOWSOCKS_CIPHER.</summary>
    public static readonly IReadOnlyList<string> ShadowsocksCiphers =
        new[] { "chacha20-ietf-poly1305", "aes-128-gcm", "aes-256-gcm" };

    public static GluetunBuildResult Build(GluetunBuildInput i)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TZ"] = i.Timezone,
            ["PUBLICIP_API"] = i.PublicIpApi,
        };
        var files = new List<FileToCopy>();

        if (!string.IsNullOrWhiteSpace(i.PublicIpApiToken))
            env["PUBLICIP_API_TOKEN"] = i.PublicIpApiToken;

        // --- HTTP proxy ---
        if (i.HttpProxyEnabled)
        {
            env["HTTPPROXY"] = "on";
            env["HTTPPROXY_LISTENING_ADDRESS"] = i.HttpProxyListeningAddress;
            env["HTTPPROXY_STEALTH"] = i.HttpProxyStealth ? "on" : "off";
            env["HTTPPROXY_LOG"] = i.HttpProxyLog ? "on" : "off";
            if (!string.IsNullOrWhiteSpace(i.HttpProxyUser))
                env["HTTPPROXY_USER"] = i.HttpProxyUser;
            if (!string.IsNullOrWhiteSpace(i.HttpProxyPassword))
                env["HTTPPROXY_PASSWORD"] = i.HttpProxyPassword;
        }
        else
        {
            env["HTTPPROXY"] = "off";
        }

        // --- Port forwarding ---
        env["VPN_PORT_FORWARDING"] = OnOff(i.PortForwarding);
        if (i.PortForwarding)
        {
            AddIfSet(env, "VPN_PORT_FORWARDING_PROVIDER", i.PortForwardingProvider);
            if (i.PortForwardingPortsCount > 1)
                env["VPN_PORT_FORWARDING_PORTS_COUNT"] = i.PortForwardingPortsCount.ToString();
        }

        // --- Firewall ---
        AddIfSet(env, "FIREWALL_VPN_INPUT_PORTS", i.FirewallVpnInputPorts);
        AddIfSet(env, "FIREWALL_OUTBOUND_SUBNETS", i.FirewallOutboundSubnets);

        // --- DNS filtering (Gluetun's internal DNS server) ---
        // Always emitted so a toggle turned back off actually reverts, rather than inheriting
        // whatever the image happens to default to.
        env["BLOCK_MALICIOUS"] = OnOff(i.BlockMalicious);
        env["BLOCK_ADS"] = OnOff(i.BlockAds);
        AddIfSet(env, "DNS_UNBLOCK_HOSTNAMES", i.DnsUnblockHostnames);

        // --- Shadowsocks (Gluetun's built-in server; listens on TCP+UDP) ---
        if (i.ShadowsocksEnabled)
        {
            env["SHADOWSOCKS"] = "on";
            env["SHADOWSOCKS_LISTENING_ADDRESS"] = $":{ShadowsocksPort}";
            env["SHADOWSOCKS_LOG"] = i.ShadowsocksLog ? "on" : "off";
            env["SHADOWSOCKS_CIPHER"] = string.IsNullOrWhiteSpace(i.ShadowsocksCipher)
                ? DefaultShadowsocksCipher
                : i.ShadowsocksCipher.Trim();
            AddIfSet(env, "SHADOWSOCKS_PASSWORD", i.ShadowsocksPassword);
        }
        else
        {
            env["SHADOWSOCKS"] = "off";
        }

        // --- Control-server auth ---
        // Always mounted: current Gluetun denies control-server routes that no role covers, so even
        // "no authentication" needs an explicit role file.
        var authToml = GluetunAuthConfig.Build(i.ControlAuth, i.ControlUser, i.ControlPassword, i.ControlApiKey);
        env["HTTP_CONTROL_SERVER_AUTH_CONFIG_FILEPATH"] = ControlConfigPath;
        files.Add(new FileToCopy(ControlConfigPath, authToml));

        // --- VPN definition ---
        if (i.IsCustom)
            BuildCustom(i, env, files);
        else
            BuildProvider(i, env);

        // Applies to both provider and custom WireGuard; meaningless for OpenVPN.
        if (i.VpnType == VpnType.WireGuard && i.WireGuardMtu is > 0)
            env["WIREGUARD_MTU"] = i.WireGuardMtu.Value.ToString();

        return new GluetunBuildResult(env, files);
    }

    private static void BuildProvider(GluetunBuildInput i, Dictionary<string, string> env)
    {
        env["VPN_SERVICE_PROVIDER"] = i.ProviderType ?? string.Empty;
        env["VPN_TYPE"] = i.VpnType == VpnType.WireGuard ? "wireguard" : "openvpn";

        if (i.VpnType == VpnType.OpenVpn)
        {
            env["OPENVPN_PROTOCOL"] = ProtocolValue(i.OpenVpnProtocol);
            AddIfSet(env, "OPENVPN_USER", i.OpenVpnUser);
            AddIfSet(env, "OPENVPN_PASSWORD", i.OpenVpnPassword);
        }
        else
        {
            AddIfSet(env, "WIREGUARD_PRIVATE_KEY", i.WireGuardPrivateKey);
            AddIfSet(env, "WIREGUARD_PRESHARED_KEY", i.WireGuardPresharedKey);
            AddIfSet(env, "WIREGUARD_ADDRESSES", i.WireGuardAddresses);
        }

        AddIfSet(env, "SERVER_COUNTRIES", i.ServerCountries);
        AddIfSet(env, "SERVER_CITIES", i.ServerCities);
        AddIfSet(env, "SERVER_REGIONS", i.ServerRegions);
        AddIfSet(env, "SERVER_HOSTNAMES", i.ServerHostnames);
    }

    private static void BuildCustom(GluetunBuildInput i, Dictionary<string, string> env, List<FileToCopy> files)
    {
        env["VPN_SERVICE_PROVIDER"] = "custom";
        env["VPN_TYPE"] = i.VpnType == VpnType.WireGuard ? "wireguard" : "openvpn";

        if (i.VpnType == VpnType.OpenVpn)
        {
            env["OPENVPN_CUSTOM_CONFIG"] = CustomOpenVpnPath;
            files.Add(new FileToCopy(CustomOpenVpnPath, i.CustomRawConfig ?? string.Empty));
            AddIfSet(env, "OPENVPN_USER", i.OpenVpnUser);
            AddIfSet(env, "OPENVPN_PASSWORD", i.OpenVpnPassword);
        }
        else
        {
            var wg = WireGuardConfigParser.Parse(i.CustomRawConfig ?? string.Empty);
            AddIfSet(env, "WIREGUARD_PRIVATE_KEY", wg.PrivateKey);
            AddIfSet(env, "WIREGUARD_ADDRESSES", wg.Addresses);
            AddIfSet(env, "WIREGUARD_PUBLIC_KEY", wg.PublicKey);
            AddIfSet(env, "WIREGUARD_PRESHARED_KEY", wg.PresharedKey);
            AddIfSet(env, "WIREGUARD_ENDPOINT_IP", wg.EndpointHost);
            AddIfSet(env, "WIREGUARD_ENDPOINT_PORT", wg.EndpointPort);
            AddIfSet(env, "DNS_ADDRESS", wg.Dns);
        }
    }

    private static string ProtocolValue(OpenVpnProtocol p) => p == OpenVpnProtocol.Tcp ? "tcp" : "udp";

    private static string OnOff(bool on) => on ? "on" : "off";

    private static void AddIfSet(Dictionary<string, string> env, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            env[key] = value.Trim();
    }
}
