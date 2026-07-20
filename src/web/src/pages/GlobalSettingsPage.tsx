import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Settings, SettingsUpdate } from '../api/types'
import { Field, Input, Select } from '../components/Field'
import { Panel, Button, Banner, Toggle, Spinner, ActionButton } from '../components/ui'
import { useButtonStyle, BUTTON_STYLES } from '../context/ButtonStyleContext'
import { settingsSchema, zodErrors } from '../lib/validation'

const secretPlaceholder = (has: boolean) => (has ? '•••••••• set — blank keeps it' : 'not set')

export function GlobalSettingsPage() {
  const [settings, setSettings] = useState<Settings | null>(null)
  const [form, setForm] = useState<SettingsUpdate | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [banner, setBanner] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    void (async () => {
      const s = await api.getSettings()
      setSettings(s)
      setForm(toForm(s))
    })()
  }, [])

  if (!form || !settings) return <Spinner label="loading settings…" />

  const set = <K extends keyof SettingsUpdate>(k: K, v: SettingsUpdate[K]) =>
    setForm({ ...form, [k]: v })

  // Gluetun also accepts a comma-separated list, which it uses as fallbacks in order.
  const publicIpPresets = ['ipinfo', 'ifconfigco', 'ip2location', 'cloudflare']
  const isCustomPublicIp = !publicIpPresets.includes(form.publicIpApi)

  async function save(e: React.FormEvent) {
    e.preventDefault()
    setBanner(null)
    const parsed = settingsSchema.safeParse(form)
    if (!parsed.success) {
      setErrors(zodErrors(parsed.error))
      return
    }
    setErrors({})
    setSaving(true)
    try {
      const updated = await api.updateSettings(form!)
      setSettings(updated)
      setForm(toForm(updated))
      setBanner({ kind: 'ok', text: 'Settings saved.' })
    } catch (err) {
      setBanner({ kind: 'error', text: err instanceof ApiError ? err.message : 'Save failed.' })
    } finally {
      setSaving(false)
    }
  }

  async function generateKey() {
    const { apiKey } = await api.generateApiKey()
    set('controlServerApiKey', apiKey)
  }

  return (
    <form onSubmit={save} className="space-y-5">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Global Settings</h1>
          <p className="mt-1 text-[12px] text-muted">
            Defaults applied to every Gluetun container. Descriptions and examples are shown inline.
          </p>
        </div>
        <Button variant="primary" type="submit" disabled={saving}>
          {saving ? 'saving…' : 'save settings'}
        </Button>
      </header>

      {banner && <Banner kind={banner.kind === 'ok' ? 'ok' : 'error'}>{banner.text}</Banner>}

      <ButtonStylePanel />

      <Panel title="General">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Timezone" doc="timezone" required error={errors.timezone}>
            <Input value={form.timezone} onChange={(e) => set('timezone', e.target.value)} error={errors.timezone} />
          </Field>
          <Field label="Public IP API" doc="publicIpApi">
            <Select
              value={isCustomPublicIp ? 'custom' : form.publicIpApi}
              onChange={(e) => set('publicIpApi', e.target.value === 'custom' ? '' : e.target.value)}
            >
              <option value="ipinfo">ipinfo</option>
              <option value="ip2location">ip2location</option>
              <option value="cloudflare">cloudflare</option>
              <option value="custom">custom…</option>
            </Select>
          </Field>
          {isCustomPublicIp && (
            <Field label="Custom PUBLICIP_API" doc="publicIpApiCustom">
              <Input
                value={form.publicIpApi}
                onChange={(e) => set('publicIpApi', e.target.value)}
                placeholder="e.g. ip2location"
                autoFocus
              />
            </Field>
          )}
          <Field label="Public IP API Token" doc="publicIpApiToken">
            <Input
              type="password"
              placeholder={secretPlaceholder(settings.hasPublicIpApiToken)}
              value={form.publicIpApiToken ?? ''}
              onChange={(e) => set('publicIpApiToken', e.target.value || null)}
            />
          </Field>
        </div>
      </Panel>

      <Panel title="HTTP Proxy Defaults">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Enable HTTP proxy" doc="httpProxyEnabled">
            <Toggle checked={form.httpProxyEnabled} onChange={(v) => set('httpProxyEnabled', v)} label={form.httpProxyEnabled ? 'on' : 'off'} />
          </Field>
          <Field label="Listening address" doc="httpProxyListeningAddress">
            <Input value={form.httpProxyListeningAddress} onChange={(e) => set('httpProxyListeningAddress', e.target.value)} />
          </Field>
          <Field label="Proxy username" doc="httpProxyUser">
            <Input value={form.httpProxyUser ?? ''} onChange={(e) => set('httpProxyUser', e.target.value || null)} />
          </Field>
          <Field label="Proxy password" doc="httpProxyPassword">
            <Input
              type="password"
              placeholder={secretPlaceholder(settings.hasHttpProxyPassword)}
              value={form.httpProxyPassword ?? ''}
              onChange={(e) => set('httpProxyPassword', e.target.value || null)}
            />
          </Field>
          <Field label="Stealth mode" doc="httpProxyStealth">
            <Toggle checked={form.httpProxyStealth} onChange={(v) => set('httpProxyStealth', v)} label={form.httpProxyStealth ? 'on' : 'off'} />
          </Field>
          <Field label="Request logging" doc="httpProxyLog">
            <Toggle checked={form.httpProxyLog} onChange={(v) => set('httpProxyLog', v)} label={form.httpProxyLog ? 'on' : 'off'} />
          </Field>
        </div>
      </Panel>

      <Panel title="Control Server Authentication">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Auth mode" doc="controlServerAuth">
            <Select value={form.controlServerAuth} onChange={(e) => set('controlServerAuth', e.target.value)}>
              <option value="none">none</option>
              <option value="basic">basic</option>
              <option value="apikey">apikey</option>
            </Select>
          </Field>
          <div />
          {form.controlServerAuth === 'basic' && (
            <>
              <Field label="Username" doc="controlServerUser">
                <Input value={form.controlServerUser ?? ''} onChange={(e) => set('controlServerUser', e.target.value || null)} />
              </Field>
              <Field label="Password" doc="controlServerPassword">
                <Input
                  type="password"
                  placeholder={secretPlaceholder(settings.hasControlServerPassword)}
                  value={form.controlServerPassword ?? ''}
                  onChange={(e) => set('controlServerPassword', e.target.value || null)}
                />
              </Field>
            </>
          )}
          {form.controlServerAuth === 'apikey' && (
            <Field label="API key" doc="controlServerApiKey">
              <div className="flex gap-2">
                <Input
                  type="text"
                  placeholder={secretPlaceholder(settings.hasControlServerApiKey)}
                  value={form.controlServerApiKey ?? ''}
                  onChange={(e) => set('controlServerApiKey', e.target.value || null)}
                />
                <Button type="button" variant="amber" onClick={generateKey}>
                  generate
                </Button>
              </div>
            </Field>
          )}
        </div>
      </Panel>

      <Panel title="Docker & Images">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Docker host override" doc="dockerHost">
            <Input
              placeholder="blank = mounted socket"
              value={form.dockerHost ?? ''}
              onChange={(e) => set('dockerHost', e.target.value || null)}
            />
          </Field>
          <div className="flex items-end">
            <div className="text-[11px] text-muted">
              endpoint in use:{' '}
              <span className={settings.dockerConnected ? 'text-phosphor' : 'text-danger'}>
                {settings.dockerEndpoint}
              </span>{' '}
              ({settings.dockerConnected ? 'online' : 'offline'})
            </div>
          </div>
          <Field label="Gluetun image" doc="gluetunImage" required error={errors.gluetunImage}>
            <Input value={form.gluetunImage} onChange={(e) => set('gluetunImage', e.target.value)} error={errors.gluetunImage} />
          </Field>
          <Field label="SOCKS5 image" doc="socks5Image" required error={errors.socks5Image}>
            <Input value={form.socks5Image} onChange={(e) => set('socks5Image', e.target.value)} error={errors.socks5Image} />
          </Field>
          <Field label="SOCKS5 balancer image" doc="socks5BalancerImage" required error={errors.socks5BalancerImage}>
            <Input value={form.socks5BalancerImage} onChange={(e) => set('socks5BalancerImage', e.target.value)} error={errors.socks5BalancerImage} />
          </Field>
        </div>
      </Panel>

      <Panel title="Auto-Assign Port Ranges">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Connection range start" doc="portRange" error={errors.portRangeStart}>
            <Input
              type="number"
              value={form.portRangeStart}
              onChange={(e) => set('portRangeStart', Number(e.target.value))}
              error={errors.portRangeStart}
            />
          </Field>
          <Field label="Connection range end" error={errors.portRangeEnd}>
            <Input
              type="number"
              value={form.portRangeEnd}
              onChange={(e) => set('portRangeEnd', Number(e.target.value))}
              error={errors.portRangeEnd}
            />
          </Field>
          <Field label="Balancer range start" doc="balancerPortRange" error={errors.balancerPortRangeStart}>
            <Input
              type="number"
              value={form.balancerPortRangeStart}
              onChange={(e) => set('balancerPortRangeStart', Number(e.target.value))}
              error={errors.balancerPortRangeStart}
            />
          </Field>
          <Field label="Balancer range end" error={errors.balancerPortRangeEnd}>
            <Input
              type="number"
              value={form.balancerPortRangeEnd}
              onChange={(e) => set('balancerPortRangeEnd', Number(e.target.value))}
              error={errors.balancerPortRangeEnd}
            />
          </Field>
        </div>
      </Panel>
    </form>
  )
}

function toForm(s: Settings): SettingsUpdate {
  return {
    timezone: s.timezone,
    publicIpApi: s.publicIpApi,
    publicIpApiToken: null,
    httpProxyEnabled: s.httpProxyEnabled,
    httpProxyUser: s.httpProxyUser,
    httpProxyPassword: null,
    httpProxyListeningAddress: s.httpProxyListeningAddress,
    httpProxyStealth: s.httpProxyStealth,
    httpProxyLog: s.httpProxyLog,
    controlServerAuth: s.controlServerAuth,
    controlServerUser: s.controlServerUser,
    controlServerPassword: null,
    controlServerApiKey: null,
    dockerHost: s.dockerHost,
    gluetunImage: s.gluetunImage,
    socks5Image: s.socks5Image,
    socks5BalancerImage: s.socks5BalancerImage,
    portRangeStart: s.portRangeStart,
    portRangeEnd: s.portRangeEnd,
    balancerPortRangeStart: s.balancerPortRangeStart,
    balancerPortRangeEnd: s.balancerPortRangeEnd,
  }
}

/**
 * Client-side display preference for the row-action buttons (text / icons / ascii). Saved in this
 * browser only, so it is separate from the server-persisted settings form above and never posts.
 */
function ButtonStylePanel() {
  const { style, setStyle } = useButtonStyle()
  const active = BUTTON_STYLES.find((o) => o.value === style)

  return (
    <Panel title="Button style (this browser)">
      <div className="grid gap-3">
        <p className="text-[12px] text-muted">
          How the per-row action buttons are shown. Saved in this browser, not on the server, so it
          takes effect immediately without saving settings.
        </p>
        <div className="flex flex-wrap gap-2">
          {BUTTON_STYLES.map((o) => (
            <button
              key={o.value}
              type="button"
              onClick={() => setStyle(o.value)}
              className={`border px-3 py-1.5 text-[12px] uppercase tracking-wide transition-colors ${
                style === o.value
                  ? 'border-phosphor-dim bg-phosphor/10 text-phosphor'
                  : 'border-line text-ink hover:border-line-bright'
              }`}
            >
              {o.label}
            </button>
          ))}
        </div>
        {active && <p className="text-[11px] text-faint">{active.hint}</p>}

        <div className="flex flex-wrap items-center gap-1 border-t border-line pt-3">
          <span className="mr-2 text-[11px] uppercase tracking-widest text-muted">live preview</span>
          <ActionButton variant="primary" action="deploy" onClick={() => {}} />
          <ActionButton variant="ghost" action="start" onClick={() => {}} />
          <ActionButton variant="ghost" action="stop" onClick={() => {}} />
          <ActionButton variant="ghost" action="restart" onClick={() => {}} />
          <ActionButton variant="ghost" action="logs" onClick={() => {}} />
          <ActionButton variant="ghost" action="test" onClick={() => {}} />
          <ActionButton variant="ghost" action="info" onClick={() => {}} />
          <ActionButton variant="ghost" action="edit" onClick={() => {}} />
          <ActionButton variant="danger" action="del" onClick={() => {}} />
        </div>
      </div>
    </Panel>
  )
}
