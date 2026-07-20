import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Credential, CredentialRequest, CredentialUsage } from '../api/types'
import { Table, Th, Td, Tr } from '../components/Table'
import { Modal } from '../components/Modal'
import { Field, Input, Select } from '../components/Field'
import { Button, Banner, Spinner, EmptyRow, ActionButton } from '../components/ui'
import { RedeployPrompt } from '../components/RedeployPrompt'

const empty: CredentialRequest = {
  name: '',
  vpnType: 'openvpn',
  openVpnUser: null,
  openVpnPassword: null,
  wireGuardPrivateKey: null,
  wireGuardPresharedKey: null,
  wireGuardAddresses: null,
  notes: null,
}

export function CredentialsPage() {
  const [items, setItems] = useState<Credential[] | null>(null)
  const [editing, setEditing] = useState<Credential | null>(null)
  const [creating, setCreating] = useState(false)
  const [pageError, setPageError] = useState<string | null>(null)
  // Set after editing a credential that connections depend on, so the redeploy can be offered.
  const [affected, setAffected] = useState<{ name: string; usages: CredentialUsage[] } | null>(null)

  async function load() {
    setItems(await api.listCredentials())
  }
  useEffect(() => {
    void load()
  }, [])

  async function remove(c: Credential) {
    if (!confirm(`Delete credential "${c.name}"?`)) return
    setPageError(null)
    try {
      await api.deleteCredential(c.id)
      await load()
    } catch (err) {
      setPageError(err instanceof ApiError ? err.message : 'Delete failed.')
    }
  }

  if (!items) return <Spinner label="loading credentials…" />

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Credentials</h1>
          <p className="mt-1 text-[12px] text-muted">
            One VPN account, many providers. Store a login once and reference it instead of retyping
            it for every country.
          </p>
        </div>
        <Button variant="primary" onClick={() => setCreating(true)}>
          + new credential
        </Button>
      </header>

      {pageError && <Banner kind="error">{pageError}</Banner>}

      <Table
        head={
          <>
            <Th>Name</Th>
            <Th>Type</Th>
            <Th>Secrets</Th>
            <Th>Used by</Th>
            <Th>Notes</Th>
            <Th className="text-right">Actions</Th>
          </>
        }
      >
        {items.length === 0 ? (
          <EmptyRow colSpan={6}>
            no credentials yet — add one to share a login across providers
          </EmptyRow>
        ) : (
          items.map((c) => (
            <Tr key={c.id}>
              <Td className="text-ink">{c.name}</Td>
              <Td className="uppercase text-amber">{c.vpnType}</Td>
              <Td className="text-muted">
                {c.vpnType === 'wireguard'
                  ? c.hasWireGuardPrivateKey
                    ? `wg key ✓${c.hasWireGuardPresharedKey ? ' + psk' : ''}`
                    : 'no key'
                  : c.hasOpenVpnPassword
                    ? `${c.openVpnUser ?? '?'} / ••••`
                    : 'no password'}
              </Td>
              <Td className={c.usedBy > 0 ? 'text-phosphor' : 'text-faint'}>
                {c.usedBy > 0 ? `${c.usedBy}×` : 'unused'}
              </Td>
              <Td className="text-faint">{c.notes || '—'}</Td>
              <Td className="text-right">
                <div className="flex flex-wrap justify-end gap-1">
                  <ActionButton variant="ghost" action="edit" onClick={() => setEditing(c)} />
                  <ActionButton
                    variant="danger"
                    action="del"
                    label={c.usedBy > 0 ? 'in use — cannot delete' : 'del'}
                    disabled={c.usedBy > 0}
                    onClick={() => void remove(c)}
                  />
                </div>
              </Td>
            </Tr>
          ))
        )}
      </Table>

      {(creating || editing) && (
        <CredentialForm
          initial={editing}
          onClose={() => {
            setCreating(false)
            setEditing(null)
          }}
          onSaved={async (saved) => {
            const wasEditing = !!editing
            setCreating(false)
            setEditing(null)
            await load()
            // Only an edit can invalidate a running container; a brand-new credential has no
            // dependants yet.
            if (!wasEditing) return
            try {
              const usages = await api.credentialConnections(saved.id)
              if (usages.length > 0) setAffected({ name: saved.name, usages })
            } catch {
              /* the credential is saved either way — the prompt is a convenience */
            }
          }}
        />
      )}

      {affected && (
        <RedeployPrompt
          credentialName={affected.name}
          usages={affected.usages}
          onClose={() => {
            setAffected(null)
            void load()
          }}
        />
      )}
    </div>
  )
}

function CredentialForm({
  initial,
  onClose,
  onSaved,
}: {
  initial: Credential | null
  onClose: () => void
  onSaved: (saved: Credential) => void
}) {
  const [form, setForm] = useState<CredentialRequest>(
    initial
      ? {
          ...empty,
          name: initial.name,
          vpnType: initial.vpnType,
          openVpnUser: initial.openVpnUser,
          wireGuardAddresses: initial.wireGuardAddresses,
          notes: initial.notes,
        }
      : empty,
  )
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const set = <K extends keyof CredentialRequest>(k: K, v: CredentialRequest[K]) =>
    setForm({ ...form, [k]: v })
  const isWg = form.vpnType === 'wireguard'

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!form.name.trim()) {
      setError('Name is required.')
      return
    }
    setBusy(true)
    try {
      const saved = initial
        ? await api.updateCredential(initial.id, form)
        : await api.createCredential(form)
      onSaved(saved)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Save failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      title={initial ? `edit credential · ${initial.name}` : 'new credential'}
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
      {error && (
        <div className="mb-3">
          <Banner kind="error">{error}</Banner>
        </div>
      )}
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" doc="credentialName" required>
          <Input
            value={form.name}
            onChange={(e) => set('name', e.target.value)}
            placeholder="nordvpn-account"
          />
        </Field>
        <Field label="VPN type" doc="credentialVpnType" required>
          <Select
            value={form.vpnType}
            onChange={(e) => set('vpnType', e.target.value)}
            disabled={!!initial && initial.usedBy > 0}
          >
            <option value="openvpn">openvpn</option>
            <option value="wireguard">wireguard</option>
          </Select>
          {!!initial && initial.usedBy > 0 && (
            <p className="mt-1 text-[11px] text-faint">
              Locked while {initial.usedBy} provider(s)/config(s) use it — changing the type would
              leave them without usable secrets.
            </p>
          )}
        </Field>

        {isWg ? (
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
          </>
        ) : (
          <>
            <Field label="OpenVPN username" doc="openVpnUser">
              <Input
                value={form.openVpnUser ?? ''}
                onChange={(e) => set('openVpnUser', e.target.value || null)}
                placeholder="p1234567"
              />
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

        <div className="sm:col-span-2">
          <Field label="Notes" doc="credentialNotes">
            <Input
              value={form.notes ?? ''}
              onChange={(e) => set('notes', e.target.value || null)}
              placeholder="which account this is, where it came from…"
            />
          </Field>
        </div>

        {initial && initial.usedBy > 0 && (
          <div className="sm:col-span-2">
            <Banner kind="info">
              Changing these secrets affects {initial.usedBy} provider(s)/config(s) on their next
              deploy.
            </Banner>
          </div>
        )}
      </form>
    </Modal>
  )
}
