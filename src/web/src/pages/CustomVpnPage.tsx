import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Credential, CustomVpn, CustomVpnRequest } from '../api/types'
import { Table, Th, Td, Tr } from '../components/Table'
import { Modal } from '../components/Modal'
import { Field, Input, Select, TextArea } from '../components/Field'
import { Button, Banner, Spinner, EmptyRow, ActionButton } from '../components/ui'
import { CredentialPicker } from '../components/CredentialPicker'
import { customVpnSchema, zodErrors } from '../lib/validation'

export function CustomVpnPage() {
  const [items, setItems] = useState<CustomVpn[] | null>(null)
  const [editing, setEditing] = useState<CustomVpn | null>(null)
  const [duplicating, setDuplicating] = useState<CustomVpn | null>(null)
  const [creating, setCreating] = useState(false)
  const [credentials, setCredentials] = useState<Credential[]>([])
  const [pageError, setPageError] = useState<string | null>(null)

  async function load() {
    setItems(await api.listCustomVpn())
    void api.listCredentials().then(setCredentials).catch(() => {})
  }
  useEffect(() => {
    void load()
  }, [])

  async function remove(c: CustomVpn) {
    if (!confirm(`Delete custom config "${c.name}"?`)) return
    setPageError(null)
    try {
      await api.deleteCustomVpn(c.id)
      await load()
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Delete failed.')
    }
  }

  if (!items) return <Spinner label="loading custom configs…" />

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Custom VPN</h1>
          <p className="mt-1 text-[12px] text-muted">
            Upload or paste OpenVPN/WireGuard configs. Config text is encrypted at rest.
          </p>
        </div>
        <Button variant="primary" onClick={() => setCreating(true)}>
          + new config
        </Button>
      </header>

      {pageError && <Banner kind="error">{pageError}</Banner>}

      <Table
        head={
          <>
            <Th>Name</Th>
            <Th>Type</Th>
            <Th>Summary</Th>
            <Th>Notes</Th>
            <Th className="text-right">Actions</Th>
          </>
        }
      >
        {items.length === 0 ? (
          <EmptyRow colSpan={5}>no custom configs yet</EmptyRow>
        ) : (
          items.map((c) => (
            <Tr key={c.id}>
              <Td className="text-ink">{c.name}</Td>
              <Td className="uppercase text-amber">{c.vpnType}</Td>
              <Td className="text-muted">{c.summary}</Td>
              <Td className="text-muted">{c.notes || '—'}</Td>
              <Td className="text-right">
                <div className="flex flex-wrap justify-end gap-1">
                  <ActionButton variant="ghost" action="edit" onClick={() => setEditing(c)} />
                  <ActionButton variant="ghost" action="duplicate" onClick={() => setDuplicating(c)} />
                  <ActionButton variant="danger" action="del" onClick={() => remove(c)} />
                </div>
              </Td>
            </Tr>
          ))
        )}
      </Table>

      {(creating || editing || duplicating) && (
        <CustomForm
          initial={editing}
          prefill={duplicating}
          credentials={credentials}
          onClose={() => {
            setCreating(false)
            setEditing(null)
            setDuplicating(null)
          }}
          onSaved={async () => {
            setCreating(false)
            setEditing(null)
            setDuplicating(null)
            await load()
          }}
        />
      )}
    </div>
  )
}

function CustomForm({
  initial,
  prefill,
  credentials,
  onClose,
  onSaved,
}: {
  initial: CustomVpn | null
  /** When set (and initial is null), the form opens in create mode pre-filled from this entry. */
  prefill?: CustomVpn | null
  credentials: Credential[]
  onClose: () => void
  onSaved: () => void
}) {
  // A duplicate is a create pre-filled from an existing entry. The config text is copied too (the
  // admin can read their own via /raw); only the OpenVPN password can't, unless a credential supplies it.
  const isDuplicate = !initial && !!prefill
  const source = initial ?? prefill ?? null
  const [form, setForm] = useState<CustomVpnRequest>({
    name: isDuplicate ? `${source!.name}-copy` : source?.name ?? '',
    vpnType: source?.vpnType ?? 'openvpn',
    rawConfig: '',
    notes: source?.notes ?? null,
    openVpnUser: source?.openVpnUser ?? null,
    openVpnPassword: null,
    endpointDnsName: source?.endpointDnsName ?? null,
    credentialId: source?.credentialId ?? null,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loadingRaw, setLoadingRaw] = useState(!!source)
  const [fileName, setFileName] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  // Load the source's (decrypted) config so it can be freely edited — for both editing and
  // duplicating. A brand-new config starts blank.
  useEffect(() => {
    if (!source) return
    let active = true
    api
      .getCustomVpnRaw(source.id)
      .then((r) => active && setForm((f) => ({ ...f, rawConfig: r.rawConfig })))
      .catch((e) => active && setError(e instanceof ApiError ? e.message : 'Could not load config.'))
      .finally(() => active && setLoadingRaw(false))
    return () => {
      active = false
    }
  }, [source])

  const set = <K extends keyof CustomVpnRequest>(k: K, v: CustomVpnRequest[K]) => setForm((f) => ({ ...f, [k]: v }))
  const isOpenVpn = form.vpnType === 'openvpn'
  const usesPlaceholder = (form.rawConfig ?? '').includes('{{DNS_IP}}')

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const text = await file.text()
    setFileName(file.name)
    // Update config AND detected type together (one state update — avoids clobbering).
    const detected: 'openvpn' | 'wireguard' | null =
      /\[Interface\]/i.test(text) || file.name.endsWith('.conf')
        ? 'wireguard'
        : file.name.endsWith('.ovpn')
          ? 'openvpn'
          : null
    setForm((f) => ({ ...f, rawConfig: text, vpnType: detected ?? f.vpnType }))
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    const parsed = customVpnSchema(true).safeParse(form)
    const fieldErrors = parsed.success ? {} : zodErrors(parsed.error)
    if (usesPlaceholder && !form.endpointDnsName)
      fieldErrors.endpointDnsName = 'Config uses {{DNS_IP}} — set the endpoint DNS name.'
    if (Object.keys(fieldErrors).length) {
      setErrors(fieldErrors)
      return
    }
    setErrors({})
    setBusy(true)
    try {
      if (initial) await api.updateCustomVpn(initial.id, form)
      else await api.createCustomVpn(form)
      onSaved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Save failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      title={
        initial
          ? `edit config · ${initial.name}`
          : isDuplicate
            ? `duplicate config · ${prefill!.name}`
            : 'new custom config'
      }
      onClose={onClose}
      wide
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            cancel
          </Button>
          <Button variant="primary" onClick={submit} disabled={busy || loadingRaw}>
            {busy ? 'saving…' : 'save'}
          </Button>
        </>
      }
    >
      {isDuplicate && (
        <div className="mb-3">
          <Banner kind="info">
            Duplicated from <span className="text-ink">{prefill!.name}</span> — including its config
            text.{' '}
            {form.vpnType === 'openvpn' && !form.credentialId
              ? 'Re-enter the OpenVPN password below.'
              : 'Nothing else to fill in.'}
          </Banner>
        </div>
      )}
      {error && <div className="mb-3"><Banner kind="error">{error}</Banner></div>}
      <form onSubmit={submit} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Name" doc="customName" required error={errors.name}>
            <Input value={form.name} onChange={(e) => set('name', e.target.value)} error={errors.name} />
          </Field>
          <Field label="VPN type" doc="vpnType" required>
            <Select value={form.vpnType} onChange={(e) => set('vpnType', e.target.value)}>
              <option value="openvpn">openvpn</option>
              <option value="wireguard">wireguard</option>
            </Select>
          </Field>
        </div>

        {isOpenVpn && (
          <div className="grid gap-4 sm:grid-cols-2">
            <CredentialPicker
              credentials={credentials}
              vpnType="openvpn"
              openVpnOnly
              value={form.credentialId}
              onChange={(id) => set('credentialId', id)}
            />
          </div>
        )}

        {isOpenVpn && form.credentialId === null && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="OpenVPN username" doc="customOpenVpnUser">
              <Input value={form.openVpnUser ?? ''} onChange={(e) => set('openVpnUser', e.target.value || null)} />
            </Field>
            <Field label="OpenVPN password" doc="customOpenVpnPassword">
              <Input
                type="password"
                placeholder={initial?.hasOpenVpnPassword ? '•••• set — blank keeps it' : ''}
                value={form.openVpnPassword ?? ''}
                onChange={(e) => set('openVpnPassword', e.target.value || null)}
              />
            </Field>
          </div>
        )}

        <Field label="Endpoint DNS name" doc="endpointDnsName" error={errors.endpointDnsName}>
          <Input
            value={form.endpointDnsName ?? ''}
            onChange={(e) => set('endpointDnsName', e.target.value || null)}
            placeholder="vpn.example.com — resolved into {{DNS_IP}} before start"
            error={errors.endpointDnsName}
          />
        </Field>

        <Field
          label="Config content (upload a file or edit here)"
          doc="customRawConfig"
          required
          error={errors.rawConfig}
        >
          <div className="mb-2 flex items-center gap-2">
            <Button type="button" variant="ghost" onClick={() => fileRef.current?.click()}>
              upload file
            </Button>
            <input ref={fileRef} type="file" accept=".ovpn,.conf,.txt" className="hidden" onChange={onFile} />
            {fileName && <span className="text-[11px] text-phosphor">loaded: {fileName}</span>}
            {loadingRaw && <span className="text-[11px] text-muted">loading config…</span>}
            {usesPlaceholder && (
              <span className="text-[11px] text-amber">using {'{{DNS_IP}}'} placeholder</span>
            )}
          </div>
          <TextArea
            rows={14}
            className="font-mono text-[12px]"
            placeholder={
              'client\nremote {{DNS_IP}} 1194\nauth-user-pass\n\n— or —\n\n[Interface]\nPrivateKey = …\n[Peer]\nEndpoint = {{DNS_IP}}:51820'
            }
            value={form.rawConfig ?? ''}
            onChange={(e) => set('rawConfig', e.target.value)}
            error={errors.rawConfig}
          />
        </Field>

        <Field label="Notes">
          <Input value={form.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
        </Field>
      </form>
    </Modal>
  )
}
