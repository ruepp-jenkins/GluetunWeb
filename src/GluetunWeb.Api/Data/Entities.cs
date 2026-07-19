using System.ComponentModel.DataAnnotations;

namespace GluetunWeb.Api.Data;

/// <summary>The single dashboard administrator (hashed password, PBKDF2).</summary>
public class AdminUser
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string Username { get; set; } = "admin";
    /// <summary>PBKDF2 hash produced by <c>PasswordHasher&lt;AdminUser&gt;</c>. Never leaves the backend.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Singleton (Id = 1) row holding global Gluetun/dashboard settings.</summary>
public class GlobalSettings
{
    public int Id { get; set; } = 1;

    // --- General ---
    [MaxLength(64)]
    public string Timezone { get; set; } = "UTC"; // TZ

    // --- Public IP echo service (PUBLIC_API -> PUBLICIP_API) ---
    [MaxLength(32)]
    public string PublicIpApi { get; set; } = "ipinfo"; // PUBLICIP_API
    /// <summary>Encrypted PUBLICIP_API_TOKEN. Write-only from the UI.</summary>
    public string? PublicIpApiTokenEnc { get; set; }

    // --- HTTP proxy block ---
    public bool HttpProxyEnabled { get; set; }
    [MaxLength(64)]
    public string? HttpProxyUser { get; set; }
    public string? HttpProxyPasswordEnc { get; set; }
    [MaxLength(32)]
    public string HttpProxyListeningAddress { get; set; } = ":8888";
    public bool HttpProxyStealth { get; set; }
    public bool HttpProxyLog { get; set; }

    // --- Control server authentication ---
    public ControlServerAuth ControlServerAuth { get; set; } = ControlServerAuth.None;
    [MaxLength(64)]
    public string? ControlServerUser { get; set; }
    public string? ControlServerPasswordEnc { get; set; }
    public string? ControlServerApiKeyEnc { get; set; }

    // --- Docker / images / ports ---
    /// <summary>Optional TCP override, e.g. tcp://host:2375. Empty = local socket.</summary>
    [MaxLength(256)]
    public string? DockerHost { get; set; }
    [MaxLength(128)]
    public string GluetunImage { get; set; } = "qmcgaw/gluetun:latest";
    [MaxLength(128)]
    public string Socks5Image { get; set; } = "serjs/go-socks5-proxy";
    [MaxLength(128)]
    public string Socks5BalancerImage { get; set; } = "ruepp/socks5balancerasio:latest";
    // Host port range for VPN connections (SOCKS5/HTTP/control ports).
    public int PortRangeStart { get; set; } = 20000;
    public int PortRangeEnd { get; set; } = 21000;
    // Separate host port range for load balancers (listen/web/state ports).
    public int BalancerPortRangeStart { get; set; } = 30000;
    public int BalancerPortRangeEnd { get; set; } = 31000;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A reusable, named set of VPN credentials. Exists so the same account can back several providers
/// — e.g. one NordVPN login used by separate "nordvpn-nl" and "nordvpn-ch" providers — without
/// re-entering (and re-encrypting) the secrets each time.
///
/// Typed: an OpenVPN credential holds a username/password, a WireGuard one holds keys. Providers
/// only offer credentials matching their VPN type.
/// </summary>
public class Credential
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;
    public VpnType VpnType { get; set; } = VpnType.OpenVpn;

    // OpenVPN (encrypted)
    public string? OpenVpnUserEnc { get; set; }
    public string? OpenVpnPasswordEnc { get; set; }

    // WireGuard (encrypted, except the addresses which are not secret)
    public string? WireGuardPrivateKeyEnc { get; set; }
    public string? WireGuardPresharedKeyEnc { get; set; }
    [MaxLength(128)]
    public string? WireGuardAddresses { get; set; }

    [MaxLength(256)]
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A stored VPN provider with credentials. Secrets are encrypted at rest.</summary>
public class Provider
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;
    /// <summary>Gluetun VPN_SERVICE_PROVIDER value, e.g. "nordvpn", "mullvad", "private internet access".</summary>
    [MaxLength(64)]
    public string ProviderType { get; set; } = string.Empty;
    public VpnType VpnType { get; set; } = VpnType.OpenVpn;
    /// <summary>OPENVPN_PROTOCOL. Only applied when VpnType is OpenVpn (WireGuard is always UDP).</summary>
    public OpenVpnProtocol OpenVpnProtocol { get; set; } = OpenVpnProtocol.Udp;

    /// <summary>
    /// Optional shared credential. When set it supplies the secrets and the inline fields below are
    /// ignored, so the same account can back several providers.
    /// </summary>
    public int? CredentialId { get; set; }
    public Credential? Credential { get; set; }

    // OpenVPN credentials (encrypted)
    public string? OpenVpnUserEnc { get; set; }
    public string? OpenVpnPasswordEnc { get; set; }

    // WireGuard credentials (encrypted)
    public string? WireGuardPrivateKeyEnc { get; set; }
    public string? WireGuardPresharedKeyEnc { get; set; }
    [MaxLength(128)]
    public string? WireGuardAddresses { get; set; } // not secret

    // Server selection filters (comma-separated, not secret)
    [MaxLength(256)]
    public string? ServerCountries { get; set; }
    [MaxLength(256)]
    public string? ServerCities { get; set; }
    [MaxLength(256)]
    public string? ServerRegions { get; set; }
    [MaxLength(256)]
    public string? ServerHostnames { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A custom OpenVPN/WireGuard config uploaded or pasted by the user.</summary>
public class CustomVpnConfig
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;
    public VpnType VpnType { get; set; } = VpnType.OpenVpn;
    /// <summary>Encrypted raw config text (.ovpn or WireGuard .conf) — may contain private keys.</summary>
    public string RawConfigEnc { get; set; } = string.Empty;

    /// <summary>
    /// Optional shared credential supplying OPENVPN_USER/PASSWORD, so several custom configs from the
    /// same provider can share one login. Ignored for WireGuard, whose keys live in the config text.
    /// </summary>
    public int? CredentialId { get; set; }
    public Credential? Credential { get; set; }

    // OpenVPN auth (many providers require it even with a custom .ovpn). Encrypted.
    public string? OpenVpnUserEnc { get; set; }
    public string? OpenVpnPasswordEnc { get; set; }

    /// <summary>
    /// Optional endpoint hostname. Gluetun requires IPs, so at deploy time this is resolved and the
    /// {{DNS_IP}} placeholder in the config is replaced with the resolved address.
    /// </summary>
    [MaxLength(255)]
    public string? EndpointDnsName { get; set; }

    [MaxLength(512)]
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A managed connection = one Gluetun container (+ optional SOCKS5/HTTP proxy sidecar).</summary>
public class Connection
{
    public int Id { get; set; }
    /// <summary>Slug identifier, validated against ^[a-zA-Z0-9-]+$. Used for container names.</summary>
    [MaxLength(64)]
    public string Identifier { get; set; } = string.Empty;

    public ConnectionSource SourceType { get; set; } = ConnectionSource.Provider;
    public int? ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public int? CustomVpnConfigId { get; set; }
    public CustomVpnConfig? CustomVpnConfig { get; set; }

    // Per-connection server-selection overrides (nullable = inherit provider)
    [MaxLength(256)]
    public string? ServerCountriesOverride { get; set; }
    [MaxLength(256)]
    public string? ServerCitiesOverride { get; set; }
    [MaxLength(256)]
    public string? ServerHostnamesOverride { get; set; }

    // Proxy sidecars
    public bool EnableSocks5 { get; set; } = true;
    public bool EnableHttpProxy { get; set; }

    // Provider-assigned inbound port (PIA, ProtonVPN, PrivateVPN, Perfect Privacy). The port itself
    // is chosen by the provider and changes on reconnect — it is read back from the control server.
    public bool PortForwarding { get; set; }
    /// <summary>VPN_PORT_FORWARDING_PROVIDER — only needed when it differs from the connection's provider.</summary>
    [MaxLength(64)]
    public string? PortForwardingProvider { get; set; }
    public int PortForwardingPortsCount { get; set; } = 1;

    // Firewall
    /// <summary>FIREWALL_VPN_INPUT_PORTS — ports to accept from the VPN side (pairs with forwarding).</summary>
    [MaxLength(256)]
    public string? FirewallVpnInputPorts { get; set; }
    /// <summary>
    /// FIREWALL_OUTBOUND_SUBNETS — LAN subnets reachable from this container and anything sharing
    /// its network namespace. Without it, the SOCKS5 sidecar cannot reach your local network.
    /// </summary>
    [MaxLength(256)]
    public string? FirewallOutboundSubnets { get; set; }

    /// <summary>WIREGUARD_MTU — lower it when large transfers stall on an otherwise working tunnel.</summary>
    public int? WireGuardMtu { get; set; }

    // DNS filtering by Gluetun's internal DNS server. Defaults mirror Gluetun's own
    // (malicious on, ads off) so existing connections are unaffected.
    public bool BlockMalicious { get; set; } = true;
    public bool BlockAds { get; set; }
    /// <summary>DNS_UNBLOCK_HOSTNAMES — comma-separated allowlist for filtering false positives.</summary>
    [MaxLength(512)]
    public string? DnsUnblockHostnames { get; set; }

    // Gluetun's built-in Shadowsocks server (SHADOWSOCKS*). Runs inside the Gluetun container
    // itself — no sidecar — and listens on both TCP and UDP.
    public bool EnableShadowsocks { get; set; }
    public string? ShadowsocksPasswordEnc { get; set; }
    [MaxLength(32)]
    public string ShadowsocksCipher { get; set; } = "chacha20-ietf-poly1305";
    public bool ShadowsocksLog { get; set; }

    // SOCKS5 sidecar auth (serjs/go-socks5-proxy → PROXY_USER/PROXY_PASSWORD).
    // When both are unset, the sidecar runs with REQUIRE_AUTH=false (open proxy).
    [MaxLength(64)]
    public string? Socks5User { get; set; }
    public string? Socks5PasswordEnc { get; set; }

    /// <summary>
    /// First host port of this connection's reserved block (0 = unassigned). Every individual port
    /// is derived from it via <see cref="Services.PortLayout"/>, so enabling or disabling a proxy
    /// never moves the others.
    /// </summary>
    public int PortBlockStart { get; set; }

    // Live Docker state
    [MaxLength(128)]
    public string? ContainerId { get; set; }
    [MaxLength(128)]
    public string? Socks5ContainerId { get; set; }
    [MaxLength(32)]
    public string Status { get; set; } = "created"; // created/running/exited/error

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A Socks5BalancerAsio (ruepp/socks5balancerasio) container that load-balances across the SOCKS5
/// proxies of selected connections. Its config.json is generated and injected at deploy time.
/// </summary>
public class LoadBalancer
{
    public int Id { get; set; }
    /// <summary>Slug identifier, validated against ^[a-zA-Z0-9-]+$. Used for the container name.</summary>
    [MaxLength(64)]
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Host the balancer uses to reach upstream SOCKS5 ports (default host.docker.internal).</summary>
    [MaxLength(128)]
    public string UpstreamHost { get; set; } = "host.docker.internal";

    // Tuning (defaults mirror a typical Socks5BalancerAsio config).
    [MaxLength(32)]
    public string UpstreamSelectRule { get; set; } = "loop"; // loop | random | one_by_one | change_by_time
    public int RetryTimes { get; set; } = 3;
    public int ConnectTimeout { get; set; } = 2000;
    [MaxLength(128)]
    public string TestRemoteHost { get; set; } = "www.google.com";
    public int TestRemotePort { get; set; } = 443;
    public int TcpCheckPeriod { get; set; } = 30000;
    public int ConnectCheckPeriod { get; set; } = 300000;
    public int AdditionCheckPeriod { get; set; } = 10000;
    public int ThreadNum { get; set; } = 10;
    public int ServerChangeTime { get; set; } = 5000;

    /// <summary>
    /// First host port of this balancer's reserved block (0 = unassigned); listen/web/state are
    /// derived from it at fixed offsets. See <see cref="Services.PortLayout"/>.
    /// </summary>
    public int PortBlockStart { get; set; }

    [MaxLength(128)]
    public string? ContainerId { get; set; }
    [MaxLength(32)]
    public string Status { get; set; } = "created";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<LoadBalancerUpstream> Upstreams { get; set; } = new List<LoadBalancerUpstream>();
}

/// <summary>Join row: a connection used as an upstream by a load balancer.</summary>
public class LoadBalancerUpstream
{
    public int Id { get; set; }
    public int LoadBalancerId { get; set; }
    public LoadBalancer? LoadBalancer { get; set; }
    public int ConnectionId { get; set; }
    public Connection? Connection { get; set; }
}
