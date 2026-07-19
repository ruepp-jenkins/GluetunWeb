namespace GluetunWeb.Api.Data;

/// <summary>Underlying VPN protocol used by a provider, custom config, or connection.</summary>
public enum VpnType
{
    OpenVpn = 0,
    WireGuard = 1,
}

/// <summary>
/// Transport protocol for OpenVPN (maps to OPENVPN_PROTOCOL). WireGuard is UDP-only and has no
/// equivalent Gluetun option, so this is ignored for WireGuard providers.
/// </summary>
public enum OpenVpnProtocol
{
    Udp = 0,
    Tcp = 1,
}

/// <summary>Where a connection sources its VPN definition from.</summary>
public enum ConnectionSource
{
    Provider = 0,
    Custom = 1,
}

/// <summary>Authentication mode for the Gluetun HTTP control server (maps to config.toml).</summary>
public enum ControlServerAuth
{
    None = 0,
    Basic = 1,
    ApiKey = 2,
}

/// <summary>What an allocated host port is used for (on a connection or a load balancer).</summary>
public enum PortPurpose
{
    Socks5 = 0,
    HttpProxy = 1,
    Control = 2,
    BalancerListen = 3,
    BalancerWeb = 4,
    BalancerState = 5,
    Shadowsocks = 6,
}
