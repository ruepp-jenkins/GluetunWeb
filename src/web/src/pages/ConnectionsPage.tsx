import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type {
  Connection,
  ConnectionRequest,
  ConnectionRuntime,
  CustomVpn,
  ManagedContainer,
  Provider,
  Settings,
} from '../api/types'
import { Table, Th, Td, Tr } from '../components/Table'
import { Modal } from '../components/Modal'
import { Field, Input, Select } from '../components/Field'
import { Button, Banner, Panel, Spinner, StatusBadge, EmptyRow, Toggle, IconButton, icons } from '../components/ui'
import {
  CodeBlock,
  EndpointRow,
  InfoModal,
  InfoSection,
  NotDeployedNotice,
  tun2socksSnippet,
} from '../components/EndpointInfo'
import { TestModal } from '../components/TestModal'
import { connectionValidate } from '../lib/validation'

/** AEAD ciphers Gluetun accepts for SHADOWSOCKS_CIPHER. */
const SHADOWSOCKS_CIPHERS = ['chacha20-ietf-poly1305', 'aes-128-gcm', 'aes-256-gcm']

export function ConnectionsPage() {
  const [items, setItems] = useState<Connection[] | null>(null)
  const [providers, setProviders] = useState<Provider[]>([])
  const [customs, setCustoms] = useState<CustomVpn[]>([])
  const [editing, setEditing] = useState<Connection | null>(null)
  const [creating, setCreating] = useState(false)
  const [logsFor, setLogsFor] = useState<Connection | null>(null)
  const [infoFor, setInfoFor] = useState<Connection | null>(null)
  const [testFor, setTestFor] = useState<Connection | null>(null)
  const [settings, setSettings] = useState<Settings | null>(null)
  const [orphans, setOrphans] = useState<ManagedContainer[]>([])
  const [busyId, setBusyId] = useState<number | null>(null)
  const [removingId, setRemovingId] = useState<string | null>(null)
  const [pageError, setPageError] = useState<string | null>(null)

  async function load() {
    const [c, p, cv, mc] = await Promise.all([
      api.listConnections(),
      api.listProviders(),
      api.listCustomVpn(),
      api.listContainers().catch(() => [] as ManagedContainer[]),
    ])
    // Needed only to show the HTTP proxy's username in the info modal.
    void api.getSettings().then(setSettings).catch(() => {})
    setItems(c)
    setProviders(p)
    setCustoms(cv)
    setOrphans(mc.filter((m) => !m.known))
  }
  useEffect(() => {
    void load()
    const t = setInterval(() => void api.listConnections().then(setItems).catch(() => {}), 8000)
    return () => clearInterval(t)
  }, [])

  async function removeOrphan(m: ManagedContainer) {
    if (!confirm(`Remove container "${m.name}" (${m.shortId}) from Docker? Saved connections are unaffected.`))
      return
    setRemovingId(m.id)
    setPageError(null)
    try {
      await api.removeContainer(m.id)
      await load()
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Remove failed.')
    } finally {
      setRemovingId(null)
    }
  }

  async function act(c: Connection, fn: (id: number) => Promise<unknown>) {
    setBusyId(c.id)
    setPageError(null)
    try {
      await fn(c.id)
      await load()
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Action failed.')
    } finally {
      setBusyId(null)
    }
  }

  async function remove(c: Connection) {
    if (!confirm(`Delete connection "${c.identifier}"? This stops and removes its containers.`)) return
    await act(c, api.deleteConnection)
  }

  if (!items) return <Spinner label="loading connections…" />

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Connections</h1>
          <p className="mt-1 text-[12px] text-muted">
            Each connection = one Gluetun container + optional SOCKS5 sidecar. Ports auto-assigned.
          </p>
        </div>
        <Button variant="primary" onClick={() => setCreating(true)}>
          + new connection
        </Button>
      </header>

      {pageError && <Banner kind="error">{pageError}</Banner>}

      <Table
        head={
          <>
            <Th>Identifier</Th>
            <Th>Source</Th>
            <Th>Status</Th>
            <Th>SOCKS5</Th>
            <Th>HTTP</Th>
            <Th>SS</Th>
            <Th>Ctrl</Th>
            <Th>Port block</Th>
            <Th className="text-right">Actions</Th>
          </>
        }
      >
        {items.length === 0 ? (
          <EmptyRow colSpan={9}>no connections yet — create one and deploy it</EmptyRow>
        ) : (
          items.map((c) => {
            const busy = busyId === c.id
            const deployed = !!c.containerId
            return (
              <Tr key={c.id}>
                <Td className="text-ink">{c.identifier}</Td>
                <Td className="text-muted">
                  {c.sourceType === 'provider' ? (
                    <span className="text-cyan">{c.providerName}</span>
                  ) : (
                    <span className="text-amber">{c.customVpnName}</span>
                  )}
                </Td>
                <Td>
                  <div className="flex flex-col gap-0.5">
                    <div className="flex items-center gap-2">
                      <StatusBadge status={c.status} />
                      <VpnStateHint runtime={c.runtime} containerStatus={c.status} />
                    </div>
                    {c.runtime?.publicIp && (
                      <span className="text-[10px] text-cyan">
                        {c.runtime.publicIp}
                        {c.runtime.country && (
                          <span className="text-faint">
                            {' '}
                            · {[c.runtime.city, c.runtime.country].filter(Boolean).join(', ')}
                          </span>
                        )}
                      </span>
                    )}
                    {c.runtime?.forwardedPort && (
                      <span className="text-[10px] text-amber">fwd :{c.runtime.forwardedPort}</span>
                    )}
                  </div>
                </Td>
                <Td className="text-phosphor">{c.enableSocks5 ? c.socks5HostPort || '—' : 'off'}</Td>
                <Td className="text-phosphor">{c.enableHttpProxy ? c.httpProxyHostPort || '—' : 'off'}</Td>
                <Td className="text-phosphor">{c.enableShadowsocks ? c.shadowsocksHostPort || '—' : 'off'}</Td>
                <Td className="text-muted">{c.controlHostPort || '—'}</Td>
                <Td className="text-faint">
                  {c.portBlockStart ? `${c.portBlockStart}–${c.portBlockEnd}` : 'unassigned'}
                </Td>
                <Td>
                  <div className="flex justify-end gap-1">
                    <IconButton
                      variant="primary"
                      label={deployed ? 'redeploy' : 'deploy'}
                      icon={busy ? icons.busy : icons.deploy}
                      disabled={busy}
                      onClick={() => void act(c, api.deployConnection)}
                    />
                    <IconButton variant="ghost" label="start" icon={icons.start} disabled={busy || !deployed} onClick={() => void act(c, api.startConnection)} />
                    <IconButton variant="ghost" label="stop" icon={icons.stop} disabled={busy || !deployed} onClick={() => void act(c, api.stopConnection)} />
                    <IconButton variant="ghost" label="restart" icon={icons.restart} disabled={busy || !deployed} onClick={() => void act(c, api.restartConnection)} />
                    <IconButton variant="ghost" label="logs" icon={icons.logs} disabled={!deployed} onClick={() => setLogsFor(c)} />
                    <IconButton variant="ghost" label="test" icon={icons.test} disabled={!deployed} onClick={() => setTestFor(c)} />
                    <IconButton variant="ghost" label="info" icon={icons.info} onClick={() => setInfoFor(c)} />
                    <IconButton variant="ghost" label="edit" icon={icons.edit} onClick={() => setEditing(c)} />
                    <IconButton variant="danger" label="delete" icon={icons.del} disabled={busy} onClick={() => void remove(c)} />
                  </div>
                </Td>
              </Tr>
            )
          })
        )}
      </Table>

      {orphans.length > 0 && (
        <Panel title={`Orphaned containers · ${orphans.length}`}>
          <p className="mb-3 text-[12px] text-muted">
            These carry the <span className="text-phosphor">managed-by=gluetunweb</span> label but no
            longer match a known connection (e.g. left behind after a failed delete, or a reset
            database). Leave them as-is, or remove them from Docker.
          </p>
          <Table
            head={
              <>
                <Th>Container</Th>
                <Th>Image</Th>
                <Th>State</Th>
                <Th>Was connection</Th>
                <Th className="text-right">Action</Th>
              </>
            }
          >
            {orphans.map((m) => (
              <Tr key={m.id}>
                <Td className="text-ink">
                  {m.name} <span className="ml-1 text-faint">{m.shortId}</span>
                </Td>
                <Td className="text-muted">{m.image}</Td>
                <Td>
                  <StatusBadge status={m.state} />
                </Td>
                <Td className="text-amber">{m.connection ?? '—'}</Td>
                <Td className="text-right">
                  <div className="flex justify-end">
                    <IconButton
                      variant="danger"
                      label="remove from Docker"
                      icon={removingId === m.id ? icons.busy : icons.del}
                      disabled={removingId === m.id}
                      onClick={() => void removeOrphan(m)}
                    />
                  </div>
                </Td>
              </Tr>
            ))}
          </Table>
        </Panel>
      )}

      {(creating || editing) && (
        <ConnectionForm
          initial={editing}
          providers={providers}
          customs={customs}
          onClose={() => {
            setCreating(false)
            setEditing(null)
          }}
          onSaved={async () => {
            setCreating(false)
            setEditing(null)
            await load()
          }}
        />
      )}

      {logsFor && <LogsModal connection={logsFor} onClose={() => setLogsFor(null)} />}
      {testFor && (
        <TestModal
          title={`test · ${testFor.identifier}`}
          hint={
            testFor.enableSocks5
              ? 'Fetches the page through this connection’s SOCKS5 proxy — proving the whole path: proxy, tunnel and exit.'
              : 'This connection has no SOCKS5 proxy enabled, so there is nothing to test through.'
          }
          onRun={(url) => api.testConnection(testFor.id, url)}
          onClose={() => setTestFor(null)}
        />
      )}
      {infoFor && (
        <ConnectionInfoModal
          connection={infoFor}
          settings={settings}
          onClose={() => setInfoFor(null)}
        />
      )}
    </div>
  )
}

function ConnectionForm({
  initial,
  providers,
  customs,
  onClose,
  onSaved,
}: {
  initial: Connection | null
  providers: Provider[]
  customs: CustomVpn[]
  onClose: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState<ConnectionRequest>({
    identifier: initial?.identifier ?? '',
    sourceType: initial?.sourceType ?? 'provider',
    providerId: initial?.providerId ?? providers[0]?.id ?? null,
    customVpnConfigId: initial?.customVpnConfigId ?? customs[0]?.id ?? null,
    serverCountriesOverride: initial?.serverCountriesOverride ?? null,
    serverCitiesOverride: initial?.serverCitiesOverride ?? null,
    serverHostnamesOverride: initial?.serverHostnamesOverride ?? null,
    enableSocks5: initial?.enableSocks5 ?? true,
    enableHttpProxy: initial?.enableHttpProxy ?? false,
    socks5User: initial?.socks5User ?? null,
    socks5Password: null,
    enableShadowsocks: initial?.enableShadowsocks ?? false,
    shadowsocksPassword: null,
    shadowsocksCipher: initial?.shadowsocksCipher ?? 'chacha20-ietf-poly1305',
    shadowsocksLog: initial?.shadowsocksLog ?? false,
    portForwarding: initial?.portForwarding ?? false,
    portForwardingProvider: initial?.portForwardingProvider ?? null,
    portForwardingPortsCount: initial?.portForwardingPortsCount ?? 1,
    firewallVpnInputPorts: initial?.firewallVpnInputPorts ?? null,
    firewallOutboundSubnets: initial?.firewallOutboundSubnets ?? null,
    wireGuardMtu: initial?.wireGuardMtu ?? null,
    blockMalicious: initial?.blockMalicious ?? true,
    blockAds: initial?.blockAds ?? false,
    dnsUnblockHostnames: initial?.dnsUnblockHostnames ?? null,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const set = <K extends keyof ConnectionRequest>(k: K, v: ConnectionRequest[K]) => setForm({ ...form, [k]: v })
  const isProvider = form.sourceType === 'provider'

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    const errs = connectionValidate({
      identifier: form.identifier,
      sourceType: form.sourceType,
      providerId: form.providerId,
      customVpnConfigId: form.customVpnConfigId,
      enableShadowsocks: form.enableShadowsocks,
      shadowsocksPassword: form.shadowsocksPassword,
      hasShadowsocksPassword: initial?.hasShadowsocksPassword,
    })
    if (Object.keys(errs).length) {
      setErrors(errs)
      return
    }
    setErrors({})
    setBusy(true)
    try {
      if (initial) await api.updateConnection(initial.id, form)
      else await api.createConnection(form)
      onSaved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Save failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      title={initial ? `edit connection · ${initial.identifier}` : 'new connection'}
      onClose={onClose}
      wide
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            cancel
          </Button>
          <Button variant="primary" onClick={submit} disabled={busy}>
            {busy ? 'saving…' : 'save'}
          </Button>
        </>
      }
    >
      {error && <div className="mb-3"><Banner kind="error">{error}</Banner></div>}
      <form onSubmit={submit} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Identifier" doc="identifier" required error={errors.identifier}>
            <Input
              value={form.identifier}
              onChange={(e) => set('identifier', e.target.value)}
              error={errors.identifier}
              placeholder="se-proxy-01"
            />
          </Field>
          <Field label="Source" doc="connectionSource" required>
            <Select value={form.sourceType} onChange={(e) => set('sourceType', e.target.value)}>
              <option value="provider">provider</option>
              <option value="custom">custom</option>
            </Select>
          </Field>

          {isProvider ? (
            <Field label="Provider" required error={errors.providerId}>
              <Select
                value={form.providerId ?? ''}
                onChange={(e) => set('providerId', e.target.value ? Number(e.target.value) : null)}
                error={errors.providerId}
              >
                <option value="">— select —</option>
                {providers.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} ({p.vpnType})
                  </option>
                ))}
              </Select>
            </Field>
          ) : (
            <Field label="Custom config" required error={errors.customVpnConfigId}>
              <Select
                value={form.customVpnConfigId ?? ''}
                onChange={(e) => set('customVpnConfigId', e.target.value ? Number(e.target.value) : null)}
                error={errors.customVpnConfigId}
              >
                <option value="">— select —</option>
                {customs.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name} ({c.vpnType})
                  </option>
                ))}
              </Select>
            </Field>
          )}
          <div />
        </div>

        {isProvider && (
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Countries override" doc="serverCountries">
              <Input
                value={form.serverCountriesOverride ?? ''}
                onChange={(e) => set('serverCountriesOverride', e.target.value || null)}
                placeholder="inherit"
              />
            </Field>
            <Field label="Cities override" doc="serverCities">
              <Input
                value={form.serverCitiesOverride ?? ''}
                onChange={(e) => set('serverCitiesOverride', e.target.value || null)}
                placeholder="inherit"
              />
            </Field>
            <Field label="Hostnames override" doc="serverHostnames">
              <Input
                value={form.serverHostnamesOverride ?? ''}
                onChange={(e) => set('serverHostnamesOverride', e.target.value || null)}
                placeholder="inherit"
              />
            </Field>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Enable SOCKS5 sidecar" doc="enableSocks5">
            <Toggle checked={form.enableSocks5} onChange={(v) => set('enableSocks5', v)} label={form.enableSocks5 ? 'on' : 'off'} />
          </Field>
          <Field label="Enable HTTP proxy" doc="enableHttpProxy">
            <Toggle checked={form.enableHttpProxy} onChange={(v) => set('enableHttpProxy', v)} label={form.enableHttpProxy ? 'on' : 'off'} />
          </Field>
          <Field label="Enable Shadowsocks" doc="enableShadowsocks">
            <Toggle
              checked={form.enableShadowsocks}
              onChange={(v) => set('enableShadowsocks', v)}
              label={form.enableShadowsocks ? 'on' : 'off'}
            />
          </Field>
        </div>

        {form.enableSocks5 && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="SOCKS5 username" doc="socks5User">
              <Input
                value={form.socks5User ?? ''}
                onChange={(e) => set('socks5User', e.target.value || null)}
                placeholder="blank = no auth"
              />
            </Field>
            <Field label="SOCKS5 password" doc="socks5Password">
              <Input
                type="password"
                placeholder={initial?.hasSocks5Password ? '•••• set — blank keeps it' : 'blank = no auth'}
                value={form.socks5Password ?? ''}
                onChange={(e) => set('socks5Password', e.target.value || null)}
              />
            </Field>
          </div>
        )}

        {form.enableShadowsocks && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Shadowsocks password" doc="shadowsocksPassword" required error={errors.shadowsocksPassword}>
              <Input
                type="password"
                placeholder={initial?.hasShadowsocksPassword ? '•••• set — blank keeps it' : 'required'}
                value={form.shadowsocksPassword ?? ''}
                onChange={(e) => set('shadowsocksPassword', e.target.value || null)}
                error={errors.shadowsocksPassword}
              />
            </Field>
            <Field label="Shadowsocks cipher" doc="shadowsocksCipher">
              <Select
                value={form.shadowsocksCipher ?? 'chacha20-ietf-poly1305'}
                onChange={(e) => set('shadowsocksCipher', e.target.value)}
              >
                {SHADOWSOCKS_CIPHERS.map((x) => (
                  <option key={x} value={x}>
                    {x}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Shadowsocks logging" doc="shadowsocksLog">
              <Toggle
                checked={form.shadowsocksLog}
                onChange={(v) => set('shadowsocksLog', v)}
                label={form.shadowsocksLog ? 'on' : 'off'}
              />
            </Field>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Port forwarding" doc="portForwarding">
            <Toggle
              checked={form.portForwarding}
              onChange={(v) => set('portForwarding', v)}
              label={form.portForwarding ? 'on' : 'off'}
            />
          </Field>
          {form.portForwarding ? (
            <Field label="Ports count" doc="portForwardingPortsCount">
              <Input
                type="number"
                min={1}
                max={5}
                value={form.portForwardingPortsCount}
                onChange={(e) => set('portForwardingPortsCount', Number(e.target.value) || 1)}
              />
            </Field>
          ) : (
            <div />
          )}
          {form.portForwarding && (
            <>
              <Field label="Forwarding provider override" doc="portForwardingProvider">
                <Input
                  value={form.portForwardingProvider ?? ''}
                  onChange={(e) => set('portForwardingProvider', e.target.value || null)}
                  placeholder="blank = use the connection's provider"
                />
              </Field>
              <Field label="Firewall VPN input ports" doc="firewallVpnInputPorts">
                <Input
                  value={form.firewallVpnInputPorts ?? ''}
                  onChange={(e) => set('firewallVpnInputPorts', e.target.value || null)}
                  placeholder="6881"
                />
              </Field>
            </>
          )}
          <Field label="Firewall outbound subnets" doc="firewallOutboundSubnets">
            <Input
              value={form.firewallOutboundSubnets ?? ''}
              onChange={(e) => set('firewallOutboundSubnets', e.target.value || null)}
              placeholder="192.168.1.0/24"
            />
          </Field>
          <Field label="WireGuard MTU" doc="wireGuardMtu">
            <Input
              type="number"
              value={form.wireGuardMtu ?? ''}
              onChange={(e) => set('wireGuardMtu', e.target.value ? Number(e.target.value) : null)}
              placeholder="blank = Gluetun default"
            />
          </Field>
        </div>

        {form.portForwarding && (
          <Banner kind="info">
            Only <span className="text-phosphor">PIA</span>,{' '}
            <span className="text-phosphor">ProtonVPN</span>,{' '}
            <span className="text-phosphor">PrivateVPN</span> and{' '}
            <span className="text-phosphor">Perfect Privacy</span> support this. The provider assigns
            the port and it changes on reconnect — the current one is shown on the connection row
            once the tunnel is up.
          </Banner>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Block malicious" doc="blockMalicious">
            <Toggle
              checked={form.blockMalicious}
              onChange={(v) => set('blockMalicious', v)}
              label={form.blockMalicious ? 'on' : 'off'}
            />
          </Field>
          <Field label="Block ads" doc="blockAds">
            <Toggle
              checked={form.blockAds}
              onChange={(v) => set('blockAds', v)}
              label={form.blockAds ? 'on' : 'off'}
            />
          </Field>
          <Field label="Unblock hostnames" doc="dnsUnblockHostnames">
            <Input
              value={form.dnsUnblockHostnames ?? ''}
              onChange={(e) => set('dnsUnblockHostnames', e.target.value || null)}
              placeholder="example.com,cdn.example.org"
            />
          </Field>
        </div>

        {form.blockAds && (
          <Banner kind="info">
            DNS filtering only applies to lookups made <span className="text-phosphor">through</span>{' '}
            this connection. A client resolving names itself and then connecting via the proxy
            bypasses it — use <span className="text-phosphor">socks5h://</span> (remote DNS) so
            resolution happens at the proxy.
          </Banner>
        )}

        {initial && (
          <Banner kind="info">
            Configuration changes take effect on the next <span className="text-phosphor">deploy</span>.
          </Banner>
        )}
      </form>
    </Modal>
  )
}

function LogsModal({ connection, onClose }: { connection: Connection; onClose: () => void }) {
  const [logs, setLogs] = useState<string>('loading…')

  useEffect(() => {
    let active = true
    const load = () =>
      api
        .connectionLogs(connection.id, 300)
        .then((t) => active && setLogs(t || '(no output)'))
        .catch((e) => active && setLogs(e instanceof ApiError ? e.message : 'failed to load logs'))
    void load()
    const t = setInterval(load, 3000)
    return () => {
      active = false
      clearInterval(t)
    }
  }, [connection.id])

  return (
    <Modal title={`logs · ${connection.identifier}`} onClose={onClose} wide>
      <pre className="max-h-[60vh] overflow-auto whitespace-pre-wrap break-words bg-bg p-3 text-[11px] leading-relaxed text-ink">
        {logs}
      </pre>
    </Modal>
  )
}

/**
 * Ready-to-use endpoints for a connection, plus a tun2socks snippet for containers that cannot
 * speak a proxy themselves. Passwords are never sent to the browser, so they appear as placeholders.
 */
function ConnectionInfoModal({
  connection: c,
  settings,
  onClose,
}: {
  connection: Connection
  settings: Settings | null
  onClose: () => void
}) {
  const host = window.location.hostname
  const deployed = !!c.containerId

  const socksAuth = c.socks5User ? `${c.socks5User}:<password>@` : ''
  const socksUrl = `socks5://${socksAuth}${host}:${c.socks5HostPort}`

  const httpAuth = settings?.httpProxyUser ? `${settings.httpProxyUser}:<password>@` : ''
  const httpUrl = `http://${httpAuth}${host}:${c.httpProxyHostPort}`

  const anyProxy = c.enableSocks5 || c.enableHttpProxy || c.enableShadowsocks

  return (
    <InfoModal title={`endpoints · ${c.identifier}`} onClose={onClose}>
      {!deployed && <NotDeployedNotice />}

      <InfoSection title="Endpoints">
        {!anyProxy && (
          <Banner kind="warn">
            No proxy is enabled for this connection — edit it and turn on SOCKS5, the HTTP proxy or
            Shadowsocks to get a usable endpoint.
          </Banner>
        )}

        {c.enableSocks5 && (
          <EndpointRow
            label="SOCKS5"
            value={socksUrl}
            note={
              c.socks5User
                ? 'Replace <password> with the SOCKS5 password you set on this connection.'
                : 'No credentials set — the proxy accepts anyone who can reach the port.'
            }
          />
        )}

        {c.enableHttpProxy && (
          <EndpointRow
            label="HTTP proxy"
            value={httpUrl}
            note={
              settings?.httpProxyUser
                ? 'Credentials come from Global Settings. Replace <password> with the one you set there.'
                : 'No credentials set in Global Settings — open to anyone who can reach the port.'
            }
          />
        )}

        {c.enableShadowsocks && (
          <>
            <EndpointRow
              label="Shadowsocks"
              value={`${host}:${c.shadowsocksHostPort}`}
              note={
                <>
                  cipher <span className="text-ink">{c.shadowsocksCipher}</span> · published on TCP
                  and UDP · use the password you set on this connection
                </>
              }
            />
            <EndpointRow
              label="Shadowsocks URI"
              value={`ss://${c.shadowsocksCipher}:<password>@${host}:${c.shadowsocksHostPort}`}
              note="Most clients also accept the base64 form; substitute your password first."
            />
          </>
        )}

        <EndpointRow
          label="Gluetun control server"
          value={`http://${host}:${c.controlHostPort}`}
          note={
            <>
              A JSON API, <span className="text-ink">not a web UI</span> — Gluetun ships no dashboard,
              so the root path returns 404. The useful endpoints are linked below.
            </>
          }
        />
        <EndpointRow
          label="↳ exit IP"
          value={`http://${host}:${c.controlHostPort}/v1/publicip/ip`}
          href={deployed ? `http://${host}:${c.controlHostPort}/v1/publicip/ip` : undefined}
          note="Public IP, city and country of the tunnel exit."
        />
        <EndpointRow
          label="↳ vpn status"
          value={`http://${host}:${c.controlHostPort}/v1/vpn/status`}
          href={deployed ? `http://${host}:${c.controlHostPort}/v1/vpn/status` : undefined}
          note={
            <>
              Reports the VPN <span className="text-ink">process</span> state — it says
              <span className="text-ink"> running</span> even while retrying a failed login, so treat
              the exit IP as the real proof.
            </>
          }
        />
        {c.portForwarding && (
          <EndpointRow
            label="↳ forwarded port"
            value={`http://${host}:${c.controlHostPort}/v1/portforward`}
            href={deployed ? `http://${host}:${c.controlHostPort}/v1/portforward` : undefined}
            note="The provider-assigned inbound port; changes on reconnect."
          />
        )}
        {settings?.controlServerAuth === 'apikey' && (
          <Banner kind="warn">
            Control-server auth is set to <span className="text-ink">apikey</span>, which needs an
            <span className="text-ink"> X-API-Key</span> header — these links will return 401 in a
            browser. Use <span className="text-ink">basic</span> auth if you want to open them
            directly.
          </Banner>
        )}
      </InfoSection>

      <InfoSection title="DNS filtering">
        <div className="border border-line bg-panel-2 px-3 py-2 text-[12px]">
          <FilterLine label="malicious" on={c.blockMalicious} />
          <FilterLine label="ads" on={c.blockAds} />
          {c.dnsUnblockHostnames && (
            <p className="mt-1 text-[11px] text-faint">
              exempt: <span className="text-ink">{c.dnsUnblockHostnames}</span>
            </p>
          )}
        </div>
        <p className="text-[11px] text-faint">
          Applied by Gluetun's internal DNS server, so it only covers lookups made through this
          connection. Clients should use <span className="text-ink">socks5h://</span> so name
          resolution happens at the proxy rather than locally.
        </p>
      </InfoSection>

      {c.enableSocks5 && (
        <InfoSection title="Container that cannot use a proxy itself">
          <Banner kind="info">
            This connection's SOCKS5 proxy implements <span className="text-ink">UDP ASSOCIATE</span>
            , so UDP traffic (QUIC, DNS) tunnels too. Prefer it over a load balancer when you need
            more than TCP.
          </Banner>
          <CodeBlock
            title="docker-compose.yml"
            code={tun2socksSnippet({ name: 'someapp', proxyUrl: socksUrl })}
          />
          <Banner kind="warn">
            Recreating <span className="text-ink">someapp</span> gives it a fresh network namespace —
            restart the <span className="text-ink">-vpn</span> sidecar too, or the app runs with
            normal host internet until you do.
          </Banner>
        </InfoSection>
      )}
    </InfoModal>
  )
}

function FilterLine({ label, on }: { label: string; on: boolean }) {
  return (
    <div className="flex items-center gap-2">
      <span className={on ? 'text-phosphor' : 'text-faint'}>{on ? '[x]' : '[ ]'}</span>
      <span className={on ? 'text-ink' : 'text-faint'}>{label}</span>
    </div>
  )
}

/**
 * The container being "running" says nothing about the tunnel — Gluetun retries internally rather
 * than exiting, so a connection that never established still reads running to Docker. This surfaces
 * Gluetun's own answer next to the Docker badge, and stays quiet when they agree.
 */
function VpnStateHint({
  runtime,
  containerStatus,
}: {
  runtime: ConnectionRuntime | null
  containerStatus: string
}) {
  if (!runtime || containerStatus !== 'running') return null

  // A public IP is the only trustworthy "the tunnel works" signal: Gluetun fetches it *after* the
  // connection establishes. vpnStatus alone is not enough — it reports "running" for a VPN process
  // that is stuck retrying (verified against a connection whose credentials were rejected).
  if (runtime.publicIp) {
    return <span className="text-[10px] text-phosphor">vpn up</span>
  }
  if (runtime.vpnStatus === 'running') {
    return (
      <span className="text-[10px] text-amber" title="VPN process is up but no public IP yet — still connecting, or failing to authenticate. Check the logs.">
        connecting
      </span>
    )
  }
  if (runtime.vpnStatus) {
    return <span className="text-[10px] text-danger">vpn {runtime.vpnStatus}</span>
  }
  // No answer at all: still starting, or the control server is unreachable.
  return (
    <span className="text-[10px] text-amber" title={runtime.controlError ?? undefined}>
      vpn unknown
    </span>
  )
}
