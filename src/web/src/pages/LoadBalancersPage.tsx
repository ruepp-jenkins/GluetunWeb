import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Connection, LoadBalancer, LoadBalancerRequest } from '../api/types'
import { Table, Th, Td, Tr } from '../components/Table'
import { Modal } from '../components/Modal'
import { Field, Input, Select } from '../components/Field'
import { EnvVarDoc } from '../components/EnvVarDoc'
import { Button, Banner, Spinner, StatusBadge, EmptyRow } from '../components/ui'
import {
  CodeBlock,
  EndpointRow,
  InfoModal,
  InfoSection,
  NotDeployedNotice,
  tun2socksSnippet,
} from '../components/EndpointInfo'
import { TestModal } from '../components/TestModal'
import { identifierSchema } from '../lib/validation'

const emptyForm: LoadBalancerRequest = {
  identifier: '',
  upstreamHost: 'host.docker.internal',
  upstreamSelectRule: 'loop',
  retryTimes: 3,
  connectTimeout: 2000,
  testRemoteHost: 'www.google.com',
  testRemotePort: 443,
  tcpCheckPeriod: 30000,
  connectCheckPeriod: 300000,
  additionCheckPeriod: 10000,
  threadNum: 10,
  serverChangeTime: 5000,
  connectionIds: [],
}

/**
 * Builds the balancer web-UI URL, passing the state server (backend) address as ?backend=host:port
 * (URL-encoded). Without it the Socks5BalancerAsio UI defaults to the internal port 15010 and can't
 * reach the published state server.
 */
function balancerWebUrl(webPort: number, statePort: number) {
  const host = window.location.hostname
  const backend = encodeURIComponent(`${host}:${statePort}`)
  return `${window.location.protocol}//${host}:${webPort}/?backend=${backend}`
}

export function LoadBalancersPage() {
  const [items, setItems] = useState<LoadBalancer[] | null>(null)
  const [connections, setConnections] = useState<Connection[]>([])
  const [editing, setEditing] = useState<LoadBalancer | null>(null)
  const [creating, setCreating] = useState(false)
  const [logsFor, setLogsFor] = useState<LoadBalancer | null>(null)
  const [infoFor, setInfoFor] = useState<LoadBalancer | null>(null)
  const [testFor, setTestFor] = useState<LoadBalancer | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)
  const [pageError, setPageError] = useState<string | null>(null)

  async function load() {
    const [lb, conns] = await Promise.all([api.listLoadBalancers(), api.listConnections()])
    setItems(lb)
    setConnections(conns)
  }
  useEffect(() => {
    void load()
    const t = setInterval(() => void api.listLoadBalancers().then(setItems).catch(() => {}), 8000)
    return () => clearInterval(t)
  }, [])

  async function act(l: LoadBalancer, fn: (id: number) => Promise<unknown>) {
    setBusyId(l.id)
    setPageError(null)
    try {
      await fn(l.id)
      await load()
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Action failed.')
    } finally {
      setBusyId(null)
    }
  }

  async function remove(l: LoadBalancer) {
    if (!confirm(`Delete load balancer "${l.identifier}"? This stops and removes its container.`)) return
    await act(l, api.deleteLoadBalancer)
  }

  if (!items) return <Spinner label="loading load balancers…" />

  const socks5Connections = connections.filter((c) => c.enableSocks5)

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Load Balancers</h1>
          <p className="mt-1 text-[12px] text-muted">
            Socks5BalancerAsio containers that balance across your SOCKS5-enabled connections.
          </p>
        </div>
        <Button variant="primary" onClick={() => setCreating(true)}>
          + new balancer
        </Button>
      </header>

      {pageError && <Banner kind="error">{pageError}</Banner>}

      <Table
        head={
          <>
            <Th>Identifier</Th>
            <Th>Upstreams</Th>
            <Th>Status</Th>
            <Th>SOCKS5 endpoint</Th>
            <Th>Web UI</Th>
            <Th>Port block</Th>
            <Th className="text-right">Actions</Th>
          </>
        }
      >
        {items.length === 0 ? (
          <EmptyRow colSpan={7}>no load balancers yet — create one over your SOCKS5 connections</EmptyRow>
        ) : (
          items.map((l) => {
            const busy = busyId === l.id
            const deployed = !!l.containerId
            return (
              <Tr key={l.id}>
                <Td className="text-ink">{l.identifier}</Td>
                <Td className="text-muted">
                  {l.upstreams.length === 0
                    ? '—'
                    : l.upstreams.map((u) => (
                        <span key={u.connectionId} className={u.deployed ? 'text-cyan' : 'text-faint'}>
                          {u.identifier}{' '}
                        </span>
                      ))}
                </Td>
                <Td>
                  <StatusBadge status={l.status} />
                </Td>
                <Td className="text-phosphor">
                  {deployed && l.listenHostPort ? `${window.location.hostname}:${l.listenHostPort}` : '—'}
                </Td>
                <Td>
                  {deployed && l.webHostPort ? (
                    <a
                      href={balancerWebUrl(l.webHostPort, l.stateHostPort)}
                      target="_blank"
                      rel="noreferrer"
                      className="text-cyan underline hover:text-phosphor"
                      title={`web :${l.webHostPort} · backend :${l.stateHostPort}`}
                    >
                      :{l.webHostPort} ↗
                    </a>
                  ) : (
                    <span className="text-faint">—</span>
                  )}
                </Td>
                <Td className="text-faint">
                  {l.portBlockStart ? `${l.portBlockStart}–${l.portBlockEnd}` : 'unassigned'}
                </Td>
                <Td>
                  <div className="flex flex-wrap justify-end gap-1">
                    <Button variant="primary" disabled={busy} onClick={() => void act(l, api.deployLoadBalancer)}>
                      {busy ? '···' : deployed ? 'redeploy' : 'deploy'}
                    </Button>
                    <Button variant="ghost" disabled={busy || !deployed} onClick={() => void act(l, api.startLoadBalancer)}>
                      start
                    </Button>
                    <Button variant="ghost" disabled={busy || !deployed} onClick={() => void act(l, api.stopLoadBalancer)}>
                      stop
                    </Button>
                    <Button variant="ghost" disabled={busy || !deployed} onClick={() => void act(l, api.restartLoadBalancer)}>
                      restart
                    </Button>
                    <Button variant="ghost" disabled={!deployed} onClick={() => setTestFor(l)}>
                      test
                    </Button>
                    <Button variant="ghost" onClick={() => setInfoFor(l)}>
                      info
                    </Button>
                    <Button variant="ghost" disabled={!deployed} onClick={() => setLogsFor(l)}>
                      logs
                    </Button>
                    <Button variant="ghost" onClick={() => setEditing(l)}>
                      edit
                    </Button>
                    <Button variant="danger" disabled={busy} onClick={() => void remove(l)}>
                      del
                    </Button>
                  </div>
                </Td>
              </Tr>
            )
          })
        )}
      </Table>

      {(creating || editing) && (
        <LoadBalancerForm
          initial={editing}
          connections={socks5Connections}
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

      {logsFor && <LogsModal balancer={logsFor} onClose={() => setLogsFor(null)} />}
      {infoFor && <BalancerInfoModal balancer={infoFor} onClose={() => setInfoFor(null)} />}
      {testFor && (
        <TestModal
          title={`test · ${testFor.identifier}`}
          hint="Fetches the page through the balanced SOCKS5 listener, so it exercises whichever upstream the balancer picks. Run it a few times to see different exits."
          onRun={(url) => api.testLoadBalancer(testFor.id, url)}
          onClose={() => setTestFor(null)}
        />
      )}
    </div>
  )
}

function LoadBalancerForm({
  initial,
  connections,
  onClose,
  onSaved,
}: {
  initial: LoadBalancer | null
  connections: Connection[]
  onClose: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState<LoadBalancerRequest>(
    initial
      ? {
          identifier: initial.identifier,
          upstreamHost: initial.upstreamHost,
          upstreamSelectRule: initial.upstreamSelectRule,
          retryTimes: initial.retryTimes,
          connectTimeout: initial.connectTimeout,
          testRemoteHost: initial.testRemoteHost,
          testRemotePort: initial.testRemotePort,
          tcpCheckPeriod: initial.tcpCheckPeriod,
          connectCheckPeriod: initial.connectCheckPeriod,
          additionCheckPeriod: initial.additionCheckPeriod,
          threadNum: initial.threadNum,
          serverChangeTime: initial.serverChangeTime,
          connectionIds: initial.upstreams.map((u) => u.connectionId),
        }
      : emptyForm,
  )
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const set = <K extends keyof LoadBalancerRequest>(k: K, v: LoadBalancerRequest[K]) => setForm({ ...form, [k]: v })
  const num = (k: keyof LoadBalancerRequest) => (e: React.ChangeEvent<HTMLInputElement>) =>
    set(k, Number(e.target.value) as never)

  function toggleUpstream(id: number) {
    setForm((f) => ({
      ...f,
      connectionIds: f.connectionIds.includes(id)
        ? f.connectionIds.filter((x) => x !== id)
        : [...f.connectionIds, id],
    }))
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    const errs: Record<string, string> = {}
    const id = identifierSchema.safeParse(form.identifier)
    if (!id.success) errs.identifier = id.error.issues[0].message
    if (form.connectionIds.length === 0) errs.upstreams = 'Select at least one SOCKS5 connection.'
    if (Object.keys(errs).length) {
      setErrors(errs)
      return
    }
    setErrors({})
    setBusy(true)
    try {
      if (initial) await api.updateLoadBalancer(initial.id, form)
      else await api.createLoadBalancer(form)
      onSaved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Save failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      title={initial ? `edit balancer · ${initial.identifier}` : 'new load balancer'}
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
          <Field label="Identifier" doc="lbIdentifier" required error={errors.identifier}>
            <Input
              value={form.identifier}
              onChange={(e) => set('identifier', e.target.value)}
              error={errors.identifier}
              placeholder="main-lb"
            />
          </Field>
          <Field label="Upstream host" doc="lbUpstreamHost">
            <Input value={form.upstreamHost} onChange={(e) => set('upstreamHost', e.target.value)} />
          </Field>
        </div>

        {/* Upstream connection selection */}
        <div>
          <label className="mb-1 flex items-center gap-1 text-[11px] uppercase tracking-wide text-muted">
            Upstream connections <span className="text-danger">*</span>
          </label>
          <div className="border border-line bg-panel-2 p-2">
            {connections.length === 0 ? (
              <div className="px-1 py-2 text-[12px] text-muted">
                No SOCKS5-enabled connections yet. Create a connection with SOCKS5 enabled first.
              </div>
            ) : (
              <div className="grid gap-1 sm:grid-cols-2">
                {connections.map((c) => {
                  const checked = form.connectionIds.includes(c.id)
                  return (
                    <label
                      key={c.id}
                      className={`flex cursor-pointer items-center gap-2 border px-2 py-1 text-[12px] ${
                        checked ? 'border-phosphor-dim bg-phosphor/5 text-ink' : 'border-line text-muted hover:border-line-bright'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleUpstream(c.id)}
                        className="accent-phosphor"
                      />
                      <span className="text-ink">{c.identifier}</span>
                      <span className="ml-auto text-faint">
                        {c.containerId ? `:${c.socks5HostPort}` : 'not deployed'}
                      </span>
                    </label>
                  )
                })}
              </div>
            )}
          </div>
          {errors.upstreams && <div className="mt-1 text-[11px] text-danger">▸ {errors.upstreams}</div>}
          <EnvVarDoc doc="lbUpstreams" />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Select rule" doc="lbSelectRule">
            <Select value={form.upstreamSelectRule} onChange={(e) => set('upstreamSelectRule', e.target.value)}>
              <option value="loop">loop</option>
              <option value="random">random</option>
              <option value="one_by_one">one_by_one</option>
              <option value="change_by_time">change_by_time</option>
            </Select>
          </Field>
          <Field label="Retry times" doc="lbRetryTimes">
            <Input type="number" value={form.retryTimes} onChange={num('retryTimes')} />
          </Field>
          <Field label="Connect timeout (ms)" doc="lbConnectTimeout">
            <Input type="number" value={form.connectTimeout} onChange={num('connectTimeout')} />
          </Field>
          <Field label="Thread count" doc="lbThreadNum">
            <Input type="number" value={form.threadNum} onChange={num('threadNum')} />
          </Field>
          <Field label="Health test host" doc="lbTestRemoteHost">
            <Input value={form.testRemoteHost} onChange={(e) => set('testRemoteHost', e.target.value)} />
          </Field>
          <Field label="Health test port" doc="lbTestRemotePort">
            <Input type="number" value={form.testRemotePort} onChange={num('testRemotePort')} />
          </Field>
          <Field label="TCP check period (ms)" doc="lbTcpCheckPeriod">
            <Input type="number" value={form.tcpCheckPeriod} onChange={num('tcpCheckPeriod')} />
          </Field>
          <Field label="Connect check period (ms)" doc="lbConnectCheckPeriod">
            <Input type="number" value={form.connectCheckPeriod} onChange={num('connectCheckPeriod')} />
          </Field>
          <Field label="Addition check period (ms)" doc="lbAdditionCheckPeriod">
            <Input type="number" value={form.additionCheckPeriod} onChange={num('additionCheckPeriod')} />
          </Field>
          <Field label="Server change time (ms)" doc="lbServerChangeTime">
            <Input type="number" value={form.serverChangeTime} onChange={num('serverChangeTime')} />
          </Field>
        </div>

        {initial && (
          <Banner kind="info">
            Changes take effect on the next <span className="text-phosphor">deploy</span>. Redeploy after
            (re)deploying upstream connections so their ports are current.
          </Banner>
        )}
      </form>
    </Modal>
  )
}

function LogsModal({ balancer, onClose }: { balancer: LoadBalancer; onClose: () => void }) {
  const [logs, setLogs] = useState('loading…')
  useEffect(() => {
    let active = true
    const load = () =>
      api
        .loadBalancerLogs(balancer.id, 300)
        .then((t) => active && setLogs(t || '(no output)'))
        .catch((e) => active && setLogs(e instanceof ApiError ? e.message : 'failed to load logs'))
    void load()
    const t = setInterval(load, 3000)
    return () => {
      active = false
      clearInterval(t)
    }
  }, [balancer.id])

  return (
    <Modal title={`logs · ${balancer.identifier}`} onClose={onClose} wide>
      <pre className="max-h-[60vh] overflow-auto whitespace-pre-wrap break-words bg-bg p-3 text-[11px] leading-relaxed text-ink">
        {logs}
      </pre>
    </Modal>
  )
}

/**
 * Ready-to-use endpoints for a balancer, plus a tun2socks snippet. The prominent caveat here is
 * that Socks5BalancerAsio is TCP-oriented, so anything tunnelled through it loses UDP.
 */
function BalancerInfoModal({
  balancer: l,
  onClose,
}: {
  balancer: LoadBalancer
  onClose: () => void
}) {
  const host = window.location.hostname
  const deployed = !!l.containerId
  const socksUrl = `socks5://${host}:${l.listenHostPort}`

  return (
    <InfoModal title={`endpoints · ${l.identifier}`} onClose={onClose}>
      {!deployed && <NotDeployedNotice />}

      <InfoSection title="Endpoints">
        <EndpointRow
          label="Balanced SOCKS5"
          value={socksUrl}
          note={
            l.upstreams.length > 0 ? (
              <>
                Spreads connections across {l.upstreams.length} upstream
                {l.upstreams.length === 1 ? '' : 's'} using{' '}
                <span className="text-ink">{l.upstreamSelectRule}</span>. Credentials are applied per
                upstream — clients authenticate to the balancer itself with none.
              </>
            ) : (
              'No upstreams selected yet — add SOCKS5-enabled connections before deploying.'
            )
          }
        />
        <EndpointRow
          label="Web UI"
          value={balancerWebUrl(l.webHostPort, l.stateHostPort)}
          href={deployed ? balancerWebUrl(l.webHostPort, l.stateHostPort) : undefined}
          note={
            deployed
              ? "Socks5BalancerAsio's status dashboard, with its backend port pre-filled."
              : "Socks5BalancerAsio's status dashboard — available once deployed."
          }
        />
      </InfoSection>

      <InfoSection title="Container that cannot use a proxy itself">
        <Banner kind="warn">
          <span className="text-ink">TCP only.</span> Socks5BalancerAsio does not implement SOCKS5
          UDP ASSOCIATE, so anything tunnelled through this balancer loses UDP — QUIC, plain UDP and
          DNS over UDP will fail while TCP keeps working, which tends to look like "the internet is
          weirdly broken" rather than an outage. If the app needs UDP, point tun2socks at a single
          connection's SOCKS5 endpoint instead and give up balancer failover.
        </Banner>
        <CodeBlock
          title="docker-compose.yml"
          code={tun2socksSnippet({ name: 'someapp', proxyUrl: socksUrl })}
        />
        <Banner kind="info">
          Use <span className="text-ink">socks5://</span> here rather than the balancer's web port —
          that one is a status dashboard, not a proxy.
        </Banner>
      </InfoSection>
    </InfoModal>
  )
}
