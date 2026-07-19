import type { Credential } from '../api/types'
import { Field, Select } from './Field'
import { Banner } from './ui'

/**
 * Chooses between a shared credential and secrets typed into this form. Credentials are filtered by
 * VPN type, since an OpenVPN provider given WireGuard keys would deploy with nothing usable and only
 * fail at connect time.
 */
export function CredentialPicker({
  credentials,
  vpnType,
  value,
  onChange,
  /** Custom configs can only take OpenVPN credentials — their WireGuard keys live in the config text. */
  openVpnOnly = false,
}: {
  credentials: Credential[]
  vpnType: string
  value: number | null
  onChange: (id: number | null) => void
  openVpnOnly?: boolean
}) {
  const wanted = openVpnOnly ? 'openvpn' : vpnType
  const usable = credentials.filter((c) => c.vpnType === wanted)
  const selected = value !== null ? credentials.find((c) => c.id === value) ?? null : null

  return (
    <>
      <Field label="Credentials" doc="credentialSource">
        <Select
          value={value === null ? 'inline' : String(value)}
          onChange={(e) => onChange(e.target.value === 'inline' ? null : Number(e.target.value))}
        >
          <option value="inline">enter directly below</option>
          {usable.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
              {c.openVpnUser ? ` · ${c.openVpnUser}` : ''}
            </option>
          ))}
        </Select>
        {usable.length === 0 && (
          <p className="mt-1 text-[11px] text-faint">
            No saved {wanted} credentials yet — add one on the Credentials page to reuse it across
            several {openVpnOnly ? 'configs' : 'providers'}.
          </p>
        )}
      </Field>

      {selected && (
        <div className="sm:col-span-2">
          <Banner kind="info">
            Using credential <span className="text-ink">{selected.name}</span>. Its secrets are
            applied at deploy time and the fields below are ignored.
          </Banner>
        </div>
      )}
    </>
  )
}
