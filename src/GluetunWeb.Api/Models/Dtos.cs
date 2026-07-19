namespace GluetunWeb.Api.Models;

// ---------------------------------------------------------------------------
// Convention for secrets:
//   * Responses NEVER contain secret values — only `has*` presence booleans.
//   * On update requests, a null secret field means "leave unchanged"; a
//     non-null value (including "") sets/clears it.
// ---------------------------------------------------------------------------

// ===== Auth =====
public record AuthStatusResponse(bool NeedsSetup, bool Authenticated, string? Username);
public record SetupRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ===== Global settings =====
public record SettingsResponse(
    string Timezone,
    string PublicIpApi,
    bool HasPublicIpApiToken,
    bool HttpProxyEnabled,
    string? HttpProxyUser,
    bool HasHttpProxyPassword,
    string HttpProxyListeningAddress,
    bool HttpProxyStealth,
    bool HttpProxyLog,
    string ControlServerAuth,
    string? ControlServerUser,
    bool HasControlServerPassword,
    bool HasControlServerApiKey,
    string? DockerHost,
    string GluetunImage,
    string Socks5Image,
    string Socks5BalancerImage,
    int PortRangeStart,
    int PortRangeEnd,
    int BalancerPortRangeStart,
    int BalancerPortRangeEnd,
    bool DockerConnected,
    string DockerEndpoint);

public record SettingsUpdateRequest(
    string Timezone,
    string PublicIpApi,
    string? PublicIpApiToken,
    bool HttpProxyEnabled,
    string? HttpProxyUser,
    string? HttpProxyPassword,
    string HttpProxyListeningAddress,
    bool HttpProxyStealth,
    bool HttpProxyLog,
    string ControlServerAuth,
    string? ControlServerUser,
    string? ControlServerPassword,
    string? ControlServerApiKey,
    string? DockerHost,
    string GluetunImage,
    string Socks5Image,
    string Socks5BalancerImage,
    int PortRangeStart,
    int PortRangeEnd,
    int BalancerPortRangeStart,
    int BalancerPortRangeEnd);

// ===== Providers =====
// ===== Credentials =====
public record CredentialResponse(
    int Id,
    string Name,
    string VpnType,
    /// <summary>Not a secret — shown so the credential is recognisable when picking one.</summary>
    string? OpenVpnUser,
    bool HasOpenVpnPassword,
    bool HasWireGuardPrivateKey,
    bool HasWireGuardPresharedKey,
    string? WireGuardAddresses,
    string? Notes,
    /// <summary>How many providers and custom configs reference it (blocks deletion when > 0).</summary>
    int UsedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// A connection whose secrets come from a credential. Used to warn — and offer a redeploy — when
/// that credential changes, since a running container keeps the old values until it is recreated.
/// </summary>
public record CredentialUsageResponse(
    int ConnectionId,
    string Identifier,
    /// <summary>Provider or custom-config name the credential reaches this connection through.</summary>
    string Via,
    string Status,
    bool Deployed,
    /// <summary>Running containers are the ones a redeploy would briefly interrupt.</summary>
    bool Running);

public record CredentialRequest(
    string Name,
    string VpnType,
    string? OpenVpnUser,
    string? OpenVpnPassword,
    string? WireGuardPrivateKey,
    string? WireGuardPresharedKey,
    string? WireGuardAddresses,
    string? Notes);

public record ProviderResponse(
    int Id,
    string Name,
    string ProviderType,
    string VpnType,
    string OpenVpnProtocol,
    int? CredentialId,
    string? CredentialName,
    string? OpenVpnUser,
    bool HasOpenVpnPassword,
    bool HasWireGuardPrivateKey,
    bool HasWireGuardPresharedKey,
    string? WireGuardAddresses,
    string? ServerCountries,
    string? ServerCities,
    string? ServerRegions,
    string? ServerHostnames,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ProviderRequest(
    string Name,
    string ProviderType,
    string VpnType,
    string? OpenVpnProtocol,
    /// <summary>When set, the inline secrets below are ignored in favour of the shared credential.</summary>
    int? CredentialId,
    string? OpenVpnUser,
    string? OpenVpnPassword,
    string? WireGuardPrivateKey,
    string? WireGuardPresharedKey,
    string? WireGuardAddresses,
    string? ServerCountries,
    string? ServerCities,
    string? ServerRegions,
    string? ServerHostnames);

// ===== Custom VPN configs =====
public record CustomVpnResponse(
    int Id,
    string Name,
    string VpnType,
    string? Notes,
    string Summary,
    int? CredentialId,
    string? CredentialName,
    string? OpenVpnUser,
    bool HasOpenVpnPassword,
    string? EndpointDnsName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CustomVpnRequest(
    string Name,
    string VpnType,
    string? RawConfig,
    string? Notes,
    string? OpenVpnUser,
    string? OpenVpnPassword,
    string? EndpointDnsName,
    /// <summary>When set, supplies OPENVPN_USER/PASSWORD instead of the inline fields.</summary>
    int? CredentialId = null);

/// <summary>The decrypted config text, returned only for the authenticated admin to edit.</summary>
public record CustomVpnRawResponse(string RawConfig);

// ===== Connections =====
public record ConnectionRuntime(
    string State,
    string? Health,
    DateTimeOffset? StartedAt,
    /// <summary>Gluetun's own view of the tunnel — null when the control server is unreachable.</summary>
    string? VpnStatus = null,
    string? PublicIp = null,
    string? Country = null,
    string? City = null,
    int? ForwardedPort = null,
    string? ControlError = null);

/// <summary>Result of fetching a URL through a connection's or balancer's SOCKS5 proxy.</summary>
public record ProxyTestResponse(
    bool Ok,
    string Url,
    string Via,
    int? StatusCode,
    string? ReasonPhrase,
    int ElapsedMs,
    string? Error,
    string? Headers,
    string? Body)
{
    public static ProxyTestResponse Failed(string url, string error) =>
        new(false, url, string.Empty, null, null, 0, error, null, null);
}

public record ProxyTestRequest(string? Url);

public record ConnectionResponse(
    int Id,
    string Identifier,
    string SourceType,
    int? ProviderId,
    string? ProviderName,
    int? CustomVpnConfigId,
    string? CustomVpnName,
    string? ServerCountriesOverride,
    string? ServerCitiesOverride,
    string? ServerHostnamesOverride,
    bool EnableSocks5,
    bool EnableHttpProxy,
    string? Socks5User,
    bool HasSocks5Password,
    int Socks5HostPort,
    int HttpProxyHostPort,
    bool PortForwarding,
    string? PortForwardingProvider,
    int PortForwardingPortsCount,
    string? FirewallVpnInputPorts,
    string? FirewallOutboundSubnets,
    int? WireGuardMtu,
    bool BlockMalicious,
    bool BlockAds,
    string? DnsUnblockHostnames,
    bool EnableShadowsocks,
    bool HasShadowsocksPassword,
    string ShadowsocksCipher,
    bool ShadowsocksLog,
    int ShadowsocksHostPort,
    int ControlHostPort,
    int PortBlockStart,
    int PortBlockEnd,
    string Status,
    string? ContainerId,
    ConnectionRuntime? Runtime,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ConnectionRequest(
    string Identifier,
    string SourceType,
    int? ProviderId,
    int? CustomVpnConfigId,
    string? ServerCountriesOverride,
    string? ServerCitiesOverride,
    string? ServerHostnamesOverride,
    bool EnableSocks5,
    bool EnableHttpProxy,
    string? Socks5User,
    string? Socks5Password,
    bool EnableShadowsocks = false,
    string? ShadowsocksPassword = null,
    string? ShadowsocksCipher = null,
    bool ShadowsocksLog = false,
    bool BlockMalicious = true,
    bool BlockAds = false,
    string? DnsUnblockHostnames = null,
    bool PortForwarding = false,
    string? PortForwardingProvider = null,
    int PortForwardingPortsCount = 1,
    string? FirewallVpnInputPorts = null,
    string? FirewallOutboundSubnets = null,
    int? WireGuardMtu = null);

// ===== Load balancers (Socks5BalancerAsio) =====
public record LoadBalancerUpstreamResponse(
    int ConnectionId,
    string Identifier,
    int Socks5HostPort,
    bool Deployed);

public record LoadBalancerResponse(
    int Id,
    string Identifier,
    string UpstreamHost,
    string UpstreamSelectRule,
    int RetryTimes,
    int ConnectTimeout,
    string TestRemoteHost,
    int TestRemotePort,
    int TcpCheckPeriod,
    int ConnectCheckPeriod,
    int AdditionCheckPeriod,
    int ThreadNum,
    int ServerChangeTime,
    int ListenHostPort,
    int WebHostPort,
    int StateHostPort,
    int PortBlockStart,
    int PortBlockEnd,
    string Status,
    string? ContainerId,
    ConnectionRuntime? Runtime,
    List<LoadBalancerUpstreamResponse> Upstreams,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record LoadBalancerRequest(
    string Identifier,
    string UpstreamHost,
    string UpstreamSelectRule,
    int RetryTimes,
    int ConnectTimeout,
    string TestRemoteHost,
    int TestRemotePort,
    int TcpCheckPeriod,
    int ConnectCheckPeriod,
    int AdditionCheckPeriod,
    int ThreadNum,
    int ServerChangeTime,
    List<int> ConnectionIds);

// ===== Managed containers (ownership detection) =====
public record ManagedContainerResponse(
    string Id,
    string ShortId,
    string Name,
    string Image,
    string State,
    string? Connection,
    bool Known);

// ===== Generic =====
public record ApiError(string Error);
public record ApiKeyResponse(string ApiKey);
