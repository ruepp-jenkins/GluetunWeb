import type {
  AuthStatus,
  Connection,
  ConnectionRequest,
  Credential,
  CredentialRequest,
  CredentialUsage,
  CustomVpn,
  CustomVpnRequest,
  LoadBalancer,
  LoadBalancerRequest,
  ManagedContainer,
  Provider,
  ProviderCatalog,
  ProviderRequest,
  ProviderServerFilter,
  ProviderServerOptions,
  ProxyTestResult,
  Settings,
  SettingsUpdate,
} from './types'

/** Error carrying the backend's { error } message and HTTP status. */
export class ApiError extends Error {
  status: number
  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(path, {
    method,
    credentials: 'same-origin',
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (res.status === 204) return undefined as T

  const text = await res.text()
  const isJson = res.headers.get('content-type')?.includes('application/json')
  const payload = isJson && text ? JSON.parse(text) : text

  if (!res.ok) {
    const message =
      isJson && payload && typeof payload === 'object' && 'error' in payload
        ? (payload as { error: string }).error
        : res.statusText || 'Request failed'
    throw new ApiError(message, res.status)
  }
  return payload as T
}

export const api = {
  // Auth
  authStatus: () => request<AuthStatus>('GET', '/api/auth/status'),
  setup: (username: string, password: string) =>
    request<AuthStatus>('POST', '/api/auth/setup', { username, password }),
  login: (username: string, password: string) =>
    request<AuthStatus>('POST', '/api/auth/login', { username, password }),
  logout: () => request<void>('POST', '/api/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>('POST', '/api/auth/change-password', { currentPassword, newPassword }),

  // Settings
  getSettings: () => request<Settings>('GET', '/api/settings'),
  updateSettings: (s: SettingsUpdate) => request<Settings>('PUT', '/api/settings', s),
  generateApiKey: () => request<{ apiKey: string }>('POST', '/api/settings/generate-apikey'),

  // Providers
  listProviders: () => request<Provider[]>('GET', '/api/providers'),
  getProviderCatalog: () => request<ProviderCatalog>('GET', '/api/providers/catalog'),
  refreshProviderCatalog: () => request<ProviderCatalog>('POST', '/api/providers/catalog/refresh'),
  getProviderServerOptions: (provider: string, filter: ProviderServerFilter = {}) => {
    const q = new URLSearchParams({ provider })
    if (filter.vpnType) q.set('vpnType', filter.vpnType)
    if (filter.regions) q.set('regions', filter.regions)
    if (filter.countries) q.set('countries', filter.countries)
    if (filter.cities) q.set('cities', filter.cities)
    return request<ProviderServerOptions>('GET', `/api/providers/catalog/servers?${q}`)
  },
  createProvider: (p: ProviderRequest) => request<Provider>('POST', '/api/providers', p),
  updateProvider: (id: number, p: ProviderRequest) => request<Provider>('PUT', `/api/providers/${id}`, p),
  deleteProvider: (id: number) => request<void>('DELETE', `/api/providers/${id}`),

  // Custom VPN
  listCustomVpn: () => request<CustomVpn[]>('GET', '/api/custom-vpn'),
  getCustomVpnRaw: (id: number) => request<{ rawConfig: string }>('GET', `/api/custom-vpn/${id}/raw`),
  createCustomVpn: (c: CustomVpnRequest) => request<CustomVpn>('POST', '/api/custom-vpn', c),
  updateCustomVpn: (id: number, c: CustomVpnRequest) => request<CustomVpn>('PUT', `/api/custom-vpn/${id}`, c),
  deleteCustomVpn: (id: number) => request<void>('DELETE', `/api/custom-vpn/${id}`),

  // Connections
  listConnections: () => request<Connection[]>('GET', '/api/connections'),
  createConnection: (c: ConnectionRequest) => request<Connection>('POST', '/api/connections', c),
  updateConnection: (id: number, c: ConnectionRequest) => request<Connection>('PUT', `/api/connections/${id}`, c),
  deleteConnection: (id: number) => request<void>('DELETE', `/api/connections/${id}`),
  deployConnection: (id: number) => request<Connection>('POST', `/api/connections/${id}/deploy`),
  startConnection: (id: number) => request<Connection>('POST', `/api/connections/${id}/start`),
  stopConnection: (id: number) => request<Connection>('POST', `/api/connections/${id}/stop`),
  restartConnection: (id: number) => request<Connection>('POST', `/api/connections/${id}/restart`),
  connectionLogs: (id: number, tail = 200) =>
    request<string>('GET', `/api/connections/${id}/logs?tail=${tail}`),
  testConnection: (id: number, url: string) =>
    request<ProxyTestResult>('POST', `/api/connections/${id}/test`, { url }),

  // Credentials (shared, reusable secrets)
  listCredentials: () => request<Credential[]>('GET', '/api/credentials'),
  createCredential: (c: CredentialRequest) => request<Credential>('POST', '/api/credentials', c),
  updateCredential: (id: number, c: CredentialRequest) =>
    request<Credential>('PUT', `/api/credentials/${id}`, c),
  deleteCredential: (id: number) => request<void>('DELETE', `/api/credentials/${id}`),
  credentialConnections: (id: number) =>
    request<CredentialUsage[]>('GET', `/api/credentials/${id}/connections`),

  // Load balancers
  listLoadBalancers: () => request<LoadBalancer[]>('GET', '/api/load-balancers'),
  createLoadBalancer: (l: LoadBalancerRequest) => request<LoadBalancer>('POST', '/api/load-balancers', l),
  updateLoadBalancer: (id: number, l: LoadBalancerRequest) =>
    request<LoadBalancer>('PUT', `/api/load-balancers/${id}`, l),
  deleteLoadBalancer: (id: number) => request<void>('DELETE', `/api/load-balancers/${id}`),
  deployLoadBalancer: (id: number) => request<LoadBalancer>('POST', `/api/load-balancers/${id}/deploy`),
  startLoadBalancer: (id: number) => request<LoadBalancer>('POST', `/api/load-balancers/${id}/start`),
  stopLoadBalancer: (id: number) => request<LoadBalancer>('POST', `/api/load-balancers/${id}/stop`),
  restartLoadBalancer: (id: number) => request<LoadBalancer>('POST', `/api/load-balancers/${id}/restart`),
  loadBalancerLogs: (id: number, tail = 200) =>
    request<string>('GET', `/api/load-balancers/${id}/logs?tail=${tail}`),
  testLoadBalancer: (id: number, url: string) =>
    request<ProxyTestResult>('POST', `/api/load-balancers/${id}/test`, { url }),

  // Managed containers (ownership detection)
  listContainers: () => request<ManagedContainer[]>('GET', '/api/containers'),
  removeContainer: (id: string) => request<void>('DELETE', `/api/containers/${id}`),
}
