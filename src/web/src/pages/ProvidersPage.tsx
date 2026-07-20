import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Credential, Provider, ProviderRequest, ProviderServerOptions } from '../api/types'
import { Table, Th, Td, Tr } from '../components/Table'
import { Modal } from '../components/Modal'
import { Field, Input, Select } from '../components/Field'
import { TagSelect } from '../components/TagSelect'
import { CredentialPicker } from '../components/CredentialPicker'
import { Button, Banner, Spinner, EmptyRow, ActionButton } from '../components/ui'
import { providerSchema, zodErrors } from '../lib/validation'

const empty: ProviderRequest = {
  name: '',
  providerType: '',
  vpnType: 'openvpn',
  openVpnProtocol: 'udp',
  credentialId: null,
  openVpnUser: null,
  openVpnPassword: null,
  wireGuardPrivateKey: null,
  wireGuardPresharedKey: null,
  wireGuardAddresses: null,
  serverCountries: null,
  serverCities: null,
  serverRegions: null,
  serverHostnames: null,
}

// Unknown provider (free-text, or the catalog is unavailable) → assume both protocols are on offer
// rather than blocking the user on missing data.
const unknownOptions: ProviderServerOptions = {
  provider: '',
  regions: [],
  countries: [],
  cities: [],
  hostnames: [],
  hostnamesTruncated: false,
  hasOpenVpn: true,
  supportsTcp: true,
  supportsUdp: true,
}

/** "Server cities · 42" — the count tells you at a glance how much the cascade has narrowed. */
const count = (label: string, options: string[]) =>
  options.length ? `${label} · ${options.length}` : label

export function ProvidersPage() {
  const [items, setItems] = useState<Provider[] | null>(null)
  const [catalog, setCatalog] = useState<string[]>([])
  const [catalogSource, setCatalogSource] = useState<string | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [credentials, setCredentials] = useState<Credential[]>([])
  const [editing, setEditing] = useState<Provider | null>(null)
  const [creating, setCreating] = useState(false)
  const [pageError, setPageError] = useState<string | null>(null)

  async function load() {
    setItems(await api.listProviders())
    void api.listCredentials().then(setCredentials).catch(() => {})
  }
  useEffect(() => {
    void load()
    void api
      .getProviderCatalog()
      .then((c) => {
        setCatalog(c.providers)
        setCatalogSource(c.source)
      })
      .catch(() => {})
  }, [])

  async function refreshCatalog() {
    setRefreshing(true)
    setPageError(null)
    try {
      const c = await api.refreshProviderCatalog()
      setCatalog(c.providers)
      setCatalogSource(c.source)
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Catalog refresh failed.')
    } finally {
      setRefreshing(false)
    }
  }

  async function remove(p: Provider) {
    if (!confirm(`Delete provider "${p.name}"?`)) return
    setPageError(null)
    try {
      await api.deleteProvider(p.id)
      await load()
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Delete failed.')
    }
  }

  if (!items) return <Spinner label="loading providers…" />

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Providers</h1>
          <p className="mt-1 text-[12px] text-muted">
            Stored VPN provider credentials. Secrets are encrypted at rest and never displayed.
          </p>
          <p className="mt-1 text-[11px] text-faint">
            {catalog.length > 0 ? (
              <>
                {catalog.length} provider names from{' '}
                <span className="text-phosphor-dim">gluetun-servers</span> (auto-refreshed)
              </>
            ) : (
              <span className="text-amber">
                provider list {catalogSource === 'unavailable' ? 'unavailable' : 'loading'} — type the
                name manually
              </span>
            )}
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="ghost" onClick={() => void refreshCatalog()} disabled={refreshing}>
            {refreshing ? 'updating…' : '↻ update list'}
          </Button>
          <Button variant="primary" onClick={() => setCreating(true)}>
            + new provider
          </Button>
        </div>
      </header>

      {pageError && <Banner kind="error">{pageError}</Banner>}

      <Table
        head={
          <>
            <Th>Name</Th>
            <Th>VPN_SERVICE_PROVIDER</Th>
            <Th>Type</Th>
            <Th>Credentials</Th>
            <Th>Server filter</Th>
            <Th className="text-right">Actions</Th>
          </>
        }
      >
        {items.length === 0 ? (
          <EmptyRow colSpan={6}>no providers yet — add one to get started</EmptyRow>
        ) : (
          items.map((p) => (
            <Tr key={p.id}>
              <Td className="text-ink">{p.name}</Td>
              <Td className="text-cyan">{p.providerType}</Td>
              <Td className="uppercase text-amber">
                {p.vpnType}
                {p.vpnType === 'openvpn' && (
                  <span className="text-muted"> · {p.openVpnProtocol}</span>
                )}
              </Td>
              <Td className="text-muted">
                {p.credentialName ? (
                  <span className="text-cyan" title="shared credential">
                    ↗ {p.credentialName}
                  </span>
                ) : p.vpnType === 'wireguard' ? (
                  p.hasWireGuardPrivateKey ? 'wg key ✓' : 'no key'
                ) : p.hasOpenVpnPassword ? (
                  `${p.openVpnUser ?? '?'} / ••••`
                ) : (
                  'no password'
                )}
              </Td>
              <Td className="text-muted">{p.serverCountries || p.serverHostnames || '—'}</Td>
              <Td className="text-right">
                <div className="flex flex-wrap justify-end gap-1">
                  <ActionButton variant="ghost" action="edit" onClick={() => setEditing(p)} />
                  <ActionButton variant="danger" action="del" onClick={() => remove(p)} />
                </div>
              </Td>
            </Tr>
          ))
        )}
      </Table>

      {(creating || editing) && (
        <ProviderForm
          initial={editing}
          providers={catalog}
          credentials={credentials}
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
    </div>
  )
}

function ProviderForm({
  initial,
  providers,
  credentials,
  onClose,
  onSaved,
}: {
  initial: Provider | null
  providers: string[]
  credentials: Credential[]
  onClose: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState<ProviderRequest>(
    initial
      ? {
          ...empty,
          name: initial.name,
          providerType: initial.providerType,
          vpnType: initial.vpnType,
          openVpnProtocol: initial.openVpnProtocol,
          credentialId: initial.credentialId,
          openVpnUser: initial.openVpnUser,
          wireGuardAddresses: initial.wireGuardAddresses,
          serverCountries: initial.serverCountries,
          serverCities: initial.serverCities,
          serverRegions: initial.serverRegions,
          serverHostnames: initial.serverHostnames,
        }
      : empty,
  )
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const set = <K extends keyof ProviderRequest>(k: K, v: ProviderRequest[K]) => setForm({ ...form, [k]: v })
  const isWg = form.vpnType === 'wireguard'
  const usesCredential = form.credentialId !== null

  // Selectable values for the current provider, narrowed by what is already chosen above. Refetched
  // whenever a higher level changes, so each dropdown only ever offers values that exist.
  const [serverOptions, setServerOptions] = useState<ProviderServerOptions>(unknownOptions)
  const providerSelected = form.providerType.trim().length > 0

  useEffect(() => {
    const p = form.providerType.trim()
    if (!p) {
      setServerOptions(unknownOptions)
      return
    }
    let active = true
    const t = setTimeout(() => {
      api
        .getProviderServerOptions(p, {
          vpnType: form.vpnType,
          regions: form.serverRegions,
          countries: form.serverCountries,
          cities: form.serverCities,
        })
        .then((o) => active && setServerOptions(o))
        .catch(() => {})
    }, 350)
    return () => {
      active = false
      clearTimeout(t)
    }
  }, [form.providerType, form.vpnType, form.serverRegions, form.serverCountries, form.serverCities])

  /**
   * Changing a level invalidates everything under it — a city from the old country would silently
   * match no server, which Gluetun only reports at connect time as "no server found".
   */
  const setLevel = (level: 'region' | 'country' | 'city' | 'hostname', value: string | null) =>
    setForm((f) => ({
      ...f,
      ...(level === 'region'
        ? { serverRegions: value, serverCountries: null, serverCities: null, serverHostnames: null }
        : level === 'country'
          ? { serverCountries: value, serverCities: null, serverHostnames: null }
          : level === 'city'
            ? { serverCities: value, serverHostnames: null }
            : { serverHostnames: value }),
    }))

  // Gluetun filters servers by OPENVPN_PROTOCOL, so an unsupported choice fails at connect time
  // with an opaque "no server found". Steer the selection to the one the provider actually offers.
  const [protocolNote, setProtocolNote] = useState<string | null>(null)
  useEffect(() => {
    if (isWg || !serverOptions.provider || !serverOptions.hasOpenVpn) {
      setProtocolNote(null)
      return
    }
    const supported = form.openVpnProtocol === 'tcp' ? serverOptions.supportsTcp : serverOptions.supportsUdp
    if (supported) {
      setProtocolNote(null)
      return
    }
    const fallback = form.openVpnProtocol === 'tcp' ? 'udp' : 'tcp'
    const fallbackOk = fallback === 'tcp' ? serverOptions.supportsTcp : serverOptions.supportsUdp
    if (fallbackOk) {
      setForm((f) => ({ ...f, openVpnProtocol: fallback }))
      setProtocolNote(
        `${serverOptions.provider} has no ${form.openVpnProtocol.toUpperCase()} servers — switched to ${fallback.toUpperCase()}.`,
      )
    }
  }, [serverOptions, form.openVpnProtocol, isWg])

  const protocolWarning =
    !isWg && serverOptions.provider && !serverOptions.hasOpenVpn
      ? `${serverOptions.provider} has no OpenVPN servers — use WireGuard instead.`
      : protocolNote

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    const parsed = providerSchema.safeParse(form)
    if (!parsed.success) {
      setErrors(zodErrors(parsed.error))
      return
    }
    setErrors({})
    setBusy(true)
    try {
      if (initial) await api.updateProvider(initial.id, form)
      else await api.createProvider(form)
      onSaved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Save failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      title={initial ? `edit provider · ${initial.name}` : 'new provider'}
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
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" doc="providerName" required error={errors.name}>
          <Input value={form.name} onChange={(e) => set('name', e.target.value)} error={errors.name} />
        </Field>
        <Field label="Provider type" doc="providerType" required error={errors.providerType}>
          <Input
            list="provider-suggestions"
            value={form.providerType}
            onChange={(e) => set('providerType', e.target.value)}
            error={errors.providerType}
            placeholder="mullvad"
          />
          <datalist id="provider-suggestions">
            {providers.map((x) => (
              <option key={x} value={x} />
            ))}
          </datalist>
        </Field>
        <Field label="VPN type" doc="vpnType" required>
          <Select value={form.vpnType} onChange={(e) => set('vpnType', e.target.value)}>
            <option value="openvpn">openvpn</option>
            <option value="wireguard">wireguard</option>
          </Select>
        </Field>
        {isWg ? (
          <div />
        ) : (
          <Field label="OpenVPN protocol" doc="openVpnProtocol" required>
            <Select
              value={form.openVpnProtocol}
              onChange={(e) => set('openVpnProtocol', e.target.value)}
            >
              <option value="udp" disabled={!serverOptions.supportsUdp}>
                udp{serverOptions.supportsUdp ? '' : ' — not offered by this provider'}
              </option>
              <option value="tcp" disabled={!serverOptions.supportsTcp}>
                tcp{serverOptions.supportsTcp ? '' : ' — not offered by this provider'}
              </option>
            </Select>
            {protocolWarning && (
              <p className="mt-1 text-xs text-amber">{protocolWarning}</p>
            )}
          </Field>
        )}

        <CredentialPicker
          credentials={credentials}
          vpnType={form.vpnType}
          value={form.credentialId}
          onChange={(id) => set('credentialId', id)}
        />

        {usesCredential ? null : isWg ? (
          <>
            <Field label="WireGuard private key" doc="wireGuardPrivateKey">
              <Input
                type="password"
                placeholder={initial?.hasWireGuardPrivateKey ? '•••• set — blank keeps it' : 'base64 key'}
                value={form.wireGuardPrivateKey ?? ''}
                onChange={(e) => set('wireGuardPrivateKey', e.target.value || null)}
              />
            </Field>
            <Field label="WireGuard addresses" doc="wireGuardAddresses">
              <Input
                value={form.wireGuardAddresses ?? ''}
                onChange={(e) => set('wireGuardAddresses', e.target.value || null)}
                placeholder="10.64.0.2/32"
              />
            </Field>
            <Field label="Pre-shared key" doc="wireGuardPresharedKey">
              <Input
                type="password"
                placeholder={initial?.hasWireGuardPresharedKey ? '•••• set — blank keeps it' : 'optional'}
                value={form.wireGuardPresharedKey ?? ''}
                onChange={(e) => set('wireGuardPresharedKey', e.target.value || null)}
              />
            </Field>
            <div />
          </>
        ) : (
          <>
            <Field label="OpenVPN username" doc="openVpnUser">
              <Input value={form.openVpnUser ?? ''} onChange={(e) => set('openVpnUser', e.target.value || null)} />
            </Field>
            <Field label="OpenVPN password" doc="openVpnPassword">
              <Input
                type="password"
                placeholder={initial?.hasOpenVpnPassword ? '•••• set — blank keeps it' : ''}
                value={form.openVpnPassword ?? ''}
                onChange={(e) => set('openVpnPassword', e.target.value || null)}
              />
            </Field>
          </>
        )}

        {providerSelected ? (
          <>
            <Field label={count('Server regions', serverOptions.regions)} doc="serverRegions">
              <TagSelect
                value={form.serverRegions}
                onChange={(v) => setLevel('region', v)}
                options={serverOptions.regions}
              />
            </Field>
            <Field label={count('Server countries', serverOptions.countries)} doc="serverCountries">
              <TagSelect
                value={form.serverCountries}
                onChange={(v) => setLevel('country', v)}
                options={serverOptions.countries}
              />
            </Field>
            <Field label={count('Server cities', serverOptions.cities)} doc="serverCities">
              <TagSelect
                value={form.serverCities}
                onChange={(v) => setLevel('city', v)}
                options={serverOptions.cities}
              />
            </Field>
            <Field label={count('Server hostnames', serverOptions.hostnames)} doc="serverHostnames">
              <TagSelect
                value={form.serverHostnames}
                onChange={(v) => setLevel('hostname', v)}
                options={serverOptions.hostnames}
              />
              {serverOptions.hostnamesTruncated && (
                <p className="mt-1 text-[11px] text-amber">
                  Showing the first {serverOptions.hostnames.length} — pick a country or city to
                  narrow the list.
                </p>
              )}
            </Field>
            <div className="sm:col-span-2">
              <p className="text-[11px] text-faint">
                Narrowed top to bottom: region → country → city → hostname. Changing one clears the
                levels below it, so a selection can never point at a server that does not exist.
              </p>
            </div>
          </>
        ) : (
          <div className="sm:col-span-2">
            <p className="text-[11px] text-faint">
              Choose a provider type above to pick regions, countries, cities and hostnames from its
              server list.
            </p>
          </div>
        )}
      </form>
    </Modal>
  )
}
