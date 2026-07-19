// Source-of-truth documentation for every configurable field. Rendered ALWAYS-VISIBLE
// beside inputs (never as tooltips), per the interface requirements. Kept in sync with
// docs/ENVVARS.md. Each entry names the underlying Gluetun env var, a plain description,
// and a concrete usage example.

export interface EnvDoc {
  /** Underlying Gluetun/Docker environment variable, when applicable. */
  env?: string
  description: string
  example?: string
}

export const envDocs = {
  // --- Global: general ---
  timezone: {
    env: 'TZ',
    description:
      'IANA timezone for container logs and scheduling. Matching your host avoids confusing timestamps.',
    example: 'Europe/Berlin',
  },
  publicIpApi: {
    env: 'PUBLICIP_API',
    description:
      'Service Gluetun queries to discover the tunnel’s public IP: ipinfo, ip2location, cloudflare — or choose Custom to supply any other value Gluetun supports.',
    example: 'ipinfo',
  },
  publicIpApiCustom: {
    env: 'PUBLICIP_API',
    description: 'Custom PUBLICIP_API value passed verbatim to Gluetun.',
    example: 'ip2location',
  },
  publicIpApiToken: {
    env: 'PUBLICIP_API_TOKEN',
    description:
      'Optional API token for the public-IP service (raises rate limits). Write-only — stored encrypted, never returned.',
    example: 'a1b2c3d4e5',
  },

  // --- Global: HTTP proxy ---
  httpProxyEnabled: {
    env: 'HTTPPROXY',
    description:
      'Runs Gluetun’s built-in HTTP proxy so other apps can route requests through the tunnel. Enabled per-connection.',
    example: 'on',
  },
  httpProxyUser: {
    env: 'HTTPPROXY_USER',
    description: 'Optional username required to use the HTTP proxy. Leave blank for no auth.',
    example: 'proxyuser',
  },
  httpProxyPassword: {
    env: 'HTTPPROXY_PASSWORD',
    description: 'Optional password for the HTTP proxy. Write-only — stored encrypted.',
    example: 's3cr3t',
  },
  httpProxyListeningAddress: {
    env: 'HTTPPROXY_LISTENING_ADDRESS',
    description: 'Address:port the HTTP proxy binds inside the container.',
    example: ':8888',
  },
  httpProxyStealth: {
    env: 'HTTPPROXY_STEALTH',
    description: 'Stealth mode hides that requests originate from a proxy (removes proxy headers).',
    example: 'on',
  },
  httpProxyLog: {
    env: 'HTTPPROXY_LOG',
    description: 'Logs each HTTP proxy request. Useful for debugging, noisy in production.',
    example: 'off',
  },

  // --- Global: control-server auth ---
  controlServerAuth: {
    env: 'HTTP_CONTROL_SERVER_AUTH_CONFIG_FILEPATH',
    description:
      'Auth for Gluetun’s control server API. none=open (trusted networks only), basic=user/password, apikey=X-API-Key header. A config.toml is generated and injected at /gluetun/auth/config.toml.',
    example: 'apikey',
  },
  controlServerUser: {
    env: 'username (config.toml)',
    description: 'Username for basic auth on the control server.',
    example: 'admin',
  },
  controlServerPassword: {
    env: 'password (config.toml)',
    description: 'Password for basic auth on the control server. Write-only — stored encrypted.',
    example: 'change-me',
  },
  controlServerApiKey: {
    env: 'apikey (config.toml)',
    description:
      'API key for the control server (X-API-Key header). Use Generate to mint a 22-char Base58 key, matching `gluetun genkey`.',
    example: 'FdaMDnTs9fiqSqkNQ4RKH7',
  },

  // --- Global: docker / images / ports ---
  dockerHost: {
    env: 'DOCKER_HOST',
    description:
      'Override the Docker/Podman endpoint. Leave blank to use the mounted socket. For Podman rootless, point at its socket (see docs/PODMAN.md).',
    example: 'tcp://10.0.0.5:2375',
  },
  gluetunImage: {
    description: 'Container image used for the VPN client.',
    example: 'qmcgaw/gluetun:latest',
  },
  socks5Image: {
    description: 'Container image used for the SOCKS5 sidecar (joined to the Gluetun network namespace).',
    example: 'serjs/go-socks5-proxy',
  },
  socks5BalancerImage: {
    description: 'Container image for the SOCKS5 load balancer (Socks5BalancerAsio).',
    example: 'ruepp/socks5balancerasio:latest',
  },
  portRange: {
    description:
      'Host port range the auto-assign manager draws from when publishing each connection’s SOCKS5/HTTP/control ports.',
    example: '20000–21000',
  },
  balancerPortRange: {
    description:
      'Separate host port range for load balancers (their listen/web-UI/state ports). Kept apart from the connection range to avoid overlap.',
    example: '30000–31000',
  },

  // --- Provider ---
  providerName: {
    description: 'A label for this stored provider. Must be unique.',
    example: 'Mullvad (WireGuard)',
  },
  providerType: {
    env: 'VPN_SERVICE_PROVIDER',
    description:
      'Gluetun’s provider identifier — suggestions come from qdm12/gluetun-servers (the exact valid names, spaces and all). Use “↻ update list” to refresh from git. Free-text is allowed for anything not listed.',
    example: 'private internet access',
  },
  vpnType: {
    env: 'VPN_TYPE',
    description: 'VPN protocol. OpenVPN is broadly compatible; WireGuard is faster and simpler.',
    example: 'openvpn',
  },
  credentialSource: {
    description:
      'Where this entry gets its secrets. Pick a saved credential to reuse one VPN account across ' +
      'several providers (nordvpn-nl, nordvpn-ch, …), or enter them directly for a one-off. A ' +
      'selected credential replaces the fields below entirely — they are not merged.',
    example: 'nordvpn-account',
  },
  credentialName: {
    description:
      'A label for this credential set, shown wherever you pick one. Name it after the account, ' +
      'not the country — the whole point is that it spans countries.',
    example: 'nordvpn-account',
  },
  credentialVpnType: {
    description:
      'Which kind of secrets this holds. OpenVPN credentials are a username/password; WireGuard ' +
      'ones are keys. Providers only offer credentials matching their own VPN type.',
    example: 'openvpn',
  },
  credentialNotes: {
    description: 'Free-text reminder of which account this is. Never sent to a container.',
    example: 'family plan, renews in March',
  },
  openVpnProtocol: {
    env: 'OPENVPN_PROTOCOL',
    description:
      'Transport for OpenVPN. UDP is faster and the default; switch to TCP when UDP is blocked ' +
      '(restrictive networks, some ISPs). Not all providers offer TCP servers — unsupported ' +
      'options are disabled below. WireGuard is UDP-only and ignores this.',
    example: 'udp',
  },
  openVpnUser: {
    env: 'OPENVPN_USER',
    description: 'OpenVPN account username from your provider (often not your login email).',
    example: 'p1234567',
  },
  openVpnPassword: {
    env: 'OPENVPN_PASSWORD',
    description: 'OpenVPN account password. Write-only — stored encrypted, never returned.',
    example: '••••••••',
  },
  wireGuardPrivateKey: {
    env: 'WIREGUARD_PRIVATE_KEY',
    description: 'WireGuard client private key (base64). Write-only — stored encrypted, never returned.',
    example: 'wOEI9rqqbDwnN8/Bpp22sVz…=',
  },
  wireGuardPresharedKey: {
    env: 'WIREGUARD_PRESHARED_KEY',
    description: 'Optional WireGuard pre-shared key for extra hardening. Write-only — stored encrypted.',
    example: 'K5f2…=',
  },
  wireGuardAddresses: {
    env: 'WIREGUARD_ADDRESSES',
    description: 'The tunnel interface address(es) assigned by your provider.',
    example: '10.64.0.2/32',
  },
  serverCountries: {
    env: 'SERVER_COUNTRIES',
    description: 'Comma-separated country filter for server selection.',
    example: 'Sweden,Netherlands',
  },
  serverCities: {
    env: 'SERVER_CITIES',
    description: 'Comma-separated city filter for server selection.',
    example: 'Amsterdam',
  },
  serverRegions: {
    env: 'SERVER_REGIONS',
    description: 'Comma-separated region filter (provider-specific).',
    example: 'Europe',
  },
  serverHostnames: {
    env: 'SERVER_HOSTNAMES',
    description: 'Comma-separated exact server hostnames to pin.',
    example: 'se-sto-wg-001',
  },

  // --- Custom VPN ---
  customName: {
    description: 'A unique label for this custom config.',
    example: 'work-openvpn',
  },
  customRawConfig: {
    env: 'OPENVPN_CUSTOM_CONFIG / WIREGUARD_*',
    description:
      'Upload a .ovpn/.conf file OR paste/edit raw text — the box is always editable. OpenVPN is injected as a custom config file; WireGuard is parsed into WIREGUARD_* variables at deploy time. Use the {{DNS_IP}} placeholder where an endpoint IP is needed (see endpoint DNS name).',
    example: 'remote {{DNS_IP}} 1194\n# or WireGuard: Endpoint = {{DNS_IP}}:51820',
  },
  customOpenVpnUser: {
    env: 'OPENVPN_USER',
    description: 'OpenVPN username for this custom config (if your provider requires auth-user-pass).',
    example: 'p1234567',
  },
  customOpenVpnPassword: {
    env: 'OPENVPN_PASSWORD',
    description: 'OpenVPN password for this custom config. Write-only — stored encrypted.',
    example: '••••••••',
  },
  endpointDnsName: {
    description:
      'Gluetun requires an IP, not a hostname. Set the endpoint hostname here and put {{DNS_IP}} in the config; it is resolved to an IP and substituted into the config just before the container starts.',
    example: 'vpn.example.com',
  },

  // --- Connection ---
  identifier: {
    description:
      'Unique slug for this connection, used in container names. Allowed: letters, digits, hyphens — pattern ^[a-zA-Z0-9-]+$.',
    example: 'se-proxy-01',
  },
  connectionSource: {
    description: 'Use a stored Provider (with server filters) or a Custom uploaded config.',
    example: 'provider',
  },
  enableSocks5: {
    description:
      'Run a SOCKS5 proxy sidecar sharing this connection’s tunnel. A host port is auto-assigned and published.',
    example: 'true',
  },
  enableHttpProxy: {
    env: 'HTTPPROXY',
    description: 'Publish Gluetun’s HTTP proxy for this connection on an auto-assigned host port.',
    example: 'true',
  },
  testUrl: {
    description:
      'Page to fetch through this proxy. Anything reachable works — pick a site you expect to be ' +
      'available from the VPN exit. A bare hostname is treated as https://.',
    example: 'https://ipwho.is/',
  },
  portForwarding: {
    env: 'VPN_PORT_FORWARDING',
    description:
      'Ask the provider for an inbound port, so traffic can reach you through the tunnel (torrents, ' +
      'game servers). Only PIA, ProtonVPN, PrivateVPN and Perfect Privacy support it. The port is ' +
      'assigned by the provider and changes on reconnect — it is shown on the connection once up.',
    example: 'on',
  },
  portForwardingProvider: {
    env: 'VPN_PORT_FORWARDING_PROVIDER',
    description:
      'Only needed when the forwarding code should differ from this connection’s provider — mainly ' +
      'for custom configs. Leave blank to use the provider above.',
    example: 'protonvpn',
  },
  portForwardingPortsCount: {
    env: 'VPN_PORT_FORWARDING_PORTS_COUNT',
    description: 'Number of ports to forward. Most providers allow 1; ProtonVPN allows up to 5.',
    example: '1',
  },
  firewallVpnInputPorts: {
    env: 'FIREWALL_VPN_INPUT_PORTS',
    description:
      'Ports to accept from the VPN side. Needed for inbound traffic to actually reach your app — ' +
      'a forwarded port is useless if the firewall still drops it.',
    example: '6881',
  },
  firewallOutboundSubnets: {
    env: 'FIREWALL_OUTBOUND_SUBNETS',
    description:
      'LAN subnets this container (and anything sharing its network namespace, e.g. the SOCKS5 ' +
      'sidecar) may reach. Without this, local addresses are unreachable once the tunnel is up — ' +
      'the usual cause of "I can’t reach my NAS through the proxy".',
    example: '192.168.1.0/24',
  },
  wireGuardMtu: {
    env: 'WIREGUARD_MTU',
    description:
      'WireGuard MTU. Lower it (1420, 1380, 1280) when the tunnel connects but large transfers ' +
      'stall — the classic "SSH works, downloads hang" symptom. Blank uses Gluetun’s default.',
    example: '1400',
  },
  blockMalicious: {
    env: 'BLOCK_MALICIOUS',
    description:
      'Block known malicious hostnames and IPs at Gluetun’s internal DNS server. On by default — ' +
      'this is Gluetun’s own default and there is little reason to turn it off.',
    example: 'on',
  },
  blockAds: {
    env: 'BLOCK_ADS',
    description:
      'Block ad hostnames at the DNS level. Off by default: ad blocking is more false-positive ' +
      'prone than malicious blocking and can break sites that load content from ad domains.',
    example: 'off',
  },
  dnsUnblockHostnames: {
    env: 'DNS_UNBLOCK_HOSTNAMES',
    description:
      'Comma-separated hostnames to exempt from the block lists above — the escape hatch when a ' +
      'list breaks something you need.',
    example: 'example.com,cdn.example.org',
  },
  enableShadowsocks: {
    env: 'SHADOWSOCKS',
    description:
      'Run Gluetun’s built-in Shadowsocks server for this connection — no sidecar container. ' +
      'A host port is auto-assigned and published on both TCP and UDP (Shadowsocks uses both). ' +
      'Point a Shadowsocks client at that host port with the password and cipher below.',
    example: 'on',
  },
  shadowsocksPassword: {
    env: 'SHADOWSOCKS_PASSWORD',
    description:
      'Password clients must present. Required — Shadowsocks has no anonymous mode, and the port ' +
      'is published on the host. Write-only: stored encrypted, never returned.',
    example: '••••••••',
  },
  shadowsocksCipher: {
    env: 'SHADOWSOCKS_CIPHER',
    description:
      'AEAD cipher for the Shadowsocks server. Must match what your client is configured for.',
    example: 'chacha20-ietf-poly1305',
  },
  shadowsocksLog: {
    env: 'SHADOWSOCKS_LOG',
    description: 'Log Shadowsocks activity to the container logs. Off by default; useful for debugging.',
    example: 'off',
  },
  socks5User: {
    env: 'PROXY_USER',
    description:
      'Optional SOCKS5 username. Set both user and password to require authentication; leave both blank to run the proxy open (REQUIRE_AUTH=false).',
    example: 'proxyuser',
  },
  socks5Password: {
    env: 'PROXY_PASSWORD',
    description: 'SOCKS5 password. Write-only — stored encrypted, never returned.',
    example: 's3cr3t',
  },

  // --- Load balancer (Socks5BalancerAsio) ---
  lbIdentifier: {
    description:
      'Unique slug for this balancer, used in the container name. Pattern ^[a-zA-Z0-9-]+$.',
    example: 'main-lb',
  },
  lbUpstreams: {
    env: 'upstream[]',
    description:
      'The SOCKS5-enabled connections to balance across. Each becomes an upstream (host = upstream host, port = the connection’s auto-assigned SOCKS5 port, plus its SOCKS5 credentials if set). Deploy a connection first so it has a port.',
    example: 'nordvpn, goosevpn, fastestvpn',
  },
  lbUpstreamHost: {
    description:
      'Host the balancer uses to reach each connection’s published SOCKS5 port. Default host.docker.internal works via a host-gateway mapping; use the host LAN IP for remote setups.',
    example: 'host.docker.internal',
  },
  lbSelectRule: {
    env: 'upstreamSelectRule',
    description:
      'How the balancer picks an upstream: loop (round-robin), random, one_by_one, or change_by_time.',
    example: 'loop',
  },
  lbRetryTimes: {
    env: 'retryTimes',
    description: 'How many upstreams to try before failing a client connection.',
    example: '3',
  },
  lbConnectTimeout: {
    env: 'connectTimeout',
    description: 'Upstream connect timeout in milliseconds.',
    example: '2000',
  },
  lbTestRemoteHost: {
    env: 'testRemoteHost',
    description: 'Host used for upstream health checks (a connect test target).',
    example: 'www.google.com',
  },
  lbTestRemotePort: {
    env: 'testRemotePort',
    description: 'Port used for upstream health checks.',
    example: '443',
  },
  lbTcpCheckPeriod: {
    env: 'tcpCheckPeriod',
    description: 'Interval (ms) between TCP liveness checks of upstreams.',
    example: '30000',
  },
  lbConnectCheckPeriod: {
    env: 'connectCheckPeriod',
    description: 'Interval (ms) between full connect tests through upstreams.',
    example: '300000',
  },
  lbAdditionCheckPeriod: {
    env: 'additionCheckPeriod',
    description: 'Interval (ms) for additional periodic checks.',
    example: '10000',
  },
  lbThreadNum: {
    env: 'threadNum',
    description: 'Worker thread count for the balancer.',
    example: '10',
  },
  lbServerChangeTime: {
    env: 'serverChangeTime',
    description: 'For time-based selection, how long (ms) to stay on one upstream.',
    example: '5000',
  },
} as const satisfies Record<string, EnvDoc>

export type EnvDocKey = keyof typeof envDocs
