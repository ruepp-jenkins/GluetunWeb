// Mirrors the backend DTOs (GluetunWeb.Api/Models/Dtos.cs).
// Secrets are never present here — only `has*` presence flags.

export interface AuthStatus {
  needsSetup: boolean
  authenticated: boolean
  username: string | null
}

export interface Settings {
  timezone: string
  publicIpApi: string
  hasPublicIpApiToken: boolean
  httpProxyEnabled: boolean
  httpProxyUser: string | null
  hasHttpProxyPassword: boolean
  httpProxyListeningAddress: string
  httpProxyStealth: boolean
  httpProxyLog: boolean
  controlServerAuth: 'none' | 'basic' | 'apikey'
  controlServerUser: string | null
  hasControlServerPassword: boolean
  hasControlServerApiKey: boolean
  dockerHost: string | null
  gluetunImage: string
  socks5Image: string
  socks5BalancerImage: string
  portRangeStart: number
  portRangeEnd: number
  balancerPortRangeStart: number
  balancerPortRangeEnd: number
  dockerConnected: boolean
  dockerEndpoint: string
}

export interface SettingsUpdate {
  timezone: string
  publicIpApi: string
  publicIpApiToken: string | null
  httpProxyEnabled: boolean
  httpProxyUser: string | null
  httpProxyPassword: string | null
  httpProxyListeningAddress: string
  httpProxyStealth: boolean
  httpProxyLog: boolean
  controlServerAuth: string
  controlServerUser: string | null
  controlServerPassword: string | null
  controlServerApiKey: string | null
  dockerHost: string | null
  gluetunImage: string
  socks5Image: string
  socks5BalancerImage: string
  portRangeStart: number
  portRangeEnd: number
  balancerPortRangeStart: number
  balancerPortRangeEnd: number
}

export type VpnType = 'openvpn' | 'wireguard'
export type OpenVpnProtocol = 'udp' | 'tcp'

/** A named, reusable credential set — one VPN account backing several providers. */
export interface Credential {
  id: number
  name: string
  vpnType: VpnType
  openVpnUser: string | null
  hasOpenVpnPassword: boolean
  hasWireGuardPrivateKey: boolean
  hasWireGuardPresharedKey: boolean
  wireGuardAddresses: string | null
  notes: string | null
  /** Providers + custom configs referencing it; deletion is blocked while > 0. */
  usedBy: number
  createdAt: string
  updatedAt: string
}

/** A connection whose secrets come from a credential — shown when that credential changes. */
export interface CredentialUsage {
  connectionId: number
  identifier: string
  /** Provider or custom-config name the credential reaches this connection through. */
  via: string
  status: string
  deployed: boolean
  /** Running containers are the ones a redeploy would briefly interrupt. */
  running: boolean
}

export interface CredentialRequest {
  name: string
  vpnType: string
  openVpnUser: string | null
  openVpnPassword: string | null
  wireGuardPrivateKey: string | null
  wireGuardPresharedKey: string | null
  wireGuardAddresses: string | null
  notes: string | null
}

export interface Provider {
  id: number
  name: string
  providerType: string
  vpnType: VpnType
  openVpnProtocol: OpenVpnProtocol
  credentialId: number | null
  credentialName: string | null
  openVpnUser: string | null
  hasOpenVpnPassword: boolean
  hasWireGuardPrivateKey: boolean
  hasWireGuardPresharedKey: boolean
  wireGuardAddresses: string | null
  serverCountries: string | null
  serverCities: string | null
  serverRegions: string | null
  serverHostnames: string | null
  createdAt: string
  updatedAt: string
}

export interface ProviderCatalog {
  providers: string[]
  source: string
  updatedAt: string | null
}

/** Cascade: each level is narrowed by the levels above it (region → country → city → hostname). */
export interface ProviderServerOptions {
  provider: string
  regions: string[]
  countries: string[]
  cities: string[]
  hostnames: string[]
  /** True when more hostnames exist than the API returns — narrow by country/city to see them. */
  hostnamesTruncated: boolean
  /** Whether the current narrowing includes OpenVPN servers at all (Mullvad is WireGuard-only). */
  hasOpenVpn: boolean
  /** Which OpenVPN transports the *currently selected* servers support. */
  supportsTcp: boolean
  supportsUdp: boolean
}

/** Selections passed to the catalog so it can narrow the levels below them. */
export interface ProviderServerFilter {
  vpnType?: string
  regions?: string | null
  countries?: string | null
  cities?: string | null
}

export interface ProviderRequest {
  name: string
  providerType: string
  vpnType: string
  openVpnProtocol: string
  credentialId: number | null
  openVpnUser: string | null
  openVpnPassword: string | null
  wireGuardPrivateKey: string | null
  wireGuardPresharedKey: string | null
  wireGuardAddresses: string | null
  serverCountries: string | null
  serverCities: string | null
  serverRegions: string | null
  serverHostnames: string | null
}

export interface CustomVpn {
  id: number
  name: string
  vpnType: VpnType
  notes: string | null
  summary: string
  credentialId: number | null
  credentialName: string | null
  openVpnUser: string | null
  hasOpenVpnPassword: boolean
  endpointDnsName: string | null
  createdAt: string
  updatedAt: string
}

export interface CustomVpnRequest {
  name: string
  vpnType: string
  rawConfig: string | null
  notes: string | null
  openVpnUser: string | null
  openVpnPassword: string | null
  endpointDnsName: string | null
  credentialId: number | null
}

export interface ConnectionRuntime {
  state: string
  health: string | null
  startedAt: string | null
  /** Gluetun's own view of the tunnel — null when its control server is unreachable. */
  vpnStatus: string | null
  publicIp: string | null
  country: string | null
  city: string | null
  forwardedPort: number | null
  controlError: string | null
}

export interface Connection {
  id: number
  identifier: string
  sourceType: 'provider' | 'custom'
  providerId: number | null
  providerName: string | null
  customVpnConfigId: number | null
  customVpnName: string | null
  serverCountriesOverride: string | null
  serverCitiesOverride: string | null
  serverHostnamesOverride: string | null
  enableSocks5: boolean
  enableHttpProxy: boolean
  socks5User: string | null
  hasSocks5Password: boolean
  socks5HostPort: number
  httpProxyHostPort: number
  portForwarding: boolean
  portForwardingProvider: string | null
  portForwardingPortsCount: number
  firewallVpnInputPorts: string | null
  firewallOutboundSubnets: string | null
  wireGuardMtu: number | null
  blockMalicious: boolean
  blockAds: boolean
  dnsUnblockHostnames: string | null
  enableShadowsocks: boolean
  hasShadowsocksPassword: boolean
  shadowsocksCipher: string
  shadowsocksLog: boolean
  shadowsocksHostPort: number
  controlHostPort: number
  /** Reserved host-port block; every port above sits at a fixed offset inside it. */
  portBlockStart: number
  portBlockEnd: number
  status: string
  containerId: string | null
  runtime: ConnectionRuntime | null
  createdAt: string
  updatedAt: string
}

export interface ConnectionRequest {
  identifier: string
  sourceType: string
  providerId: number | null
  customVpnConfigId: number | null
  serverCountriesOverride: string | null
  serverCitiesOverride: string | null
  serverHostnamesOverride: string | null
  enableSocks5: boolean
  enableHttpProxy: boolean
  socks5User: string | null
  socks5Password: string | null
  enableShadowsocks: boolean
  shadowsocksPassword: string | null
  shadowsocksCipher: string
  shadowsocksLog: boolean
  blockMalicious: boolean
  blockAds: boolean
  dnsUnblockHostnames: string | null
  portForwarding: boolean
  portForwardingProvider: string | null
  portForwardingPortsCount: number
  firewallVpnInputPorts: string | null
  firewallOutboundSubnets: string | null
  wireGuardMtu: number | null
}

/** Result of fetching a URL through a connection's or balancer's SOCKS5 proxy. */
export interface ProxyTestResult {
  ok: boolean
  url: string
  via: string
  statusCode: number | null
  reasonPhrase: string | null
  elapsedMs: number
  error: string | null
  headers: string | null
  body: string | null
}

export interface LoadBalancerUpstream {
  connectionId: number
  identifier: string
  socks5HostPort: number
  deployed: boolean
}

export interface LoadBalancer {
  id: number
  identifier: string
  upstreamHost: string
  upstreamSelectRule: string
  retryTimes: number
  connectTimeout: number
  testRemoteHost: string
  testRemotePort: number
  tcpCheckPeriod: number
  connectCheckPeriod: number
  additionCheckPeriod: number
  threadNum: number
  serverChangeTime: number
  listenHostPort: number
  webHostPort: number
  stateHostPort: number
  /** Reserved host-port block; listen/web/state sit at fixed offsets inside it. */
  portBlockStart: number
  portBlockEnd: number
  status: string
  containerId: string | null
  runtime: ConnectionRuntime | null
  upstreams: LoadBalancerUpstream[]
  createdAt: string
  updatedAt: string
}

export interface LoadBalancerRequest {
  identifier: string
  upstreamHost: string
  upstreamSelectRule: string
  retryTimes: number
  connectTimeout: number
  testRemoteHost: string
  testRemotePort: number
  tcpCheckPeriod: number
  connectCheckPeriod: number
  additionCheckPeriod: number
  threadNum: number
  serverChangeTime: number
  connectionIds: number[]
}

export interface ManagedContainer {
  id: string
  shortId: string
  name: string
  image: string
  state: string
  connection: string | null
  known: boolean
}
