import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'

interface Step {
  n: string
  title: string
  body: ReactNode
  links: { to: string; label: string }[]
  optional?: boolean
}

const steps: Step[] = [
  {
    n: '01',
    title: 'Configure global options',
    body: (
      <>
        Set the defaults every VPN container inherits — timezone, the public-IP lookup service, the
        optional HTTP-proxy block, and the control-server authentication mode. Also where the app's
        buttons and port ranges are configured. You can accept the defaults and come back later.
      </>
    ),
    links: [{ to: '/settings', label: 'Global Settings' }],
  },
  {
    n: '02',
    title: 'Add your credentials',
    body: (
      <>
        Store each VPN account <span className="text-ink">once</span> as a named credential — an
        OpenVPN username/password or a WireGuard key set. One account usually backs several providers
        (the same NordVPN login for a NL and a CH entry), so saving it here means you never retype it.
        Skip this if you'll only paste credentials directly into a provider.
      </>
    ),
    links: [{ to: '/credentials', label: 'Credentials' }],
  },
  {
    n: '03',
    title: 'Add providers or your own configs',
    body: (
      <>
        Add a <span className="text-ink">known provider</span> (Mullvad, NordVPN, ProtonVPN, PIA, …) —
        pick it from the live catalog and narrow the server by region → country → city → hostname — and
        point it at a credential from step 2. Or bring your <span className="text-ink">own</span>{' '}
        OpenVPN <code className="text-cyan">.ovpn</code> / WireGuard <code className="text-cyan">.conf</code>{' '}
        file under Custom VPN.
      </>
    ),
    links: [
      { to: '/providers', label: 'Providers' },
      { to: '/custom-vpn', label: 'Custom VPN' },
    ],
  },
  {
    n: '04',
    title: 'Create connections and connect',
    body: (
      <>
        A connection turns a provider or custom config into a running Gluetun container with an
        optional SOCKS5 / HTTP / Shadowsocks proxy. Give it a name, deploy it, and watch the status
        column for the exit IP. Use <span className="text-ink">test</span> to fetch a page through the
        proxy and <span className="text-ink">info</span> for copy-ready endpoints.
      </>
    ),
    links: [{ to: '/connections', label: 'Connections' }],
  },
  {
    n: '05',
    title: 'Bundle them in a load balancer',
    optional: true,
    body: (
      <>
        Optional: put several SOCKS5-enabled connections behind one balanced endpoint that spreads and
        health-checks across them. Handy for rotating exits or pooling bandwidth. Add it any time once
        you have a couple of connections running.
      </>
    ),
    links: [{ to: '/load-balancers', label: 'Load Balancers' }],
  },
]

export function GuidePage() {
  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-[16px] uppercase tracking-widest text-phosphor">Getting started</h1>
        <p className="mt-1 max-w-2xl text-[12px] text-muted">
          GluetunWeb manages Gluetun VPN containers and their companion proxies. Work through these
          steps in order — each links straight to the page you need. Every field on those pages shows
          its description and example inline, so you never have to leave to look things up.
        </p>
      </header>

      <ol className="space-y-3">
        {steps.map((s) => (
          <li key={s.n} className="border border-line bg-panel">
            <div className="flex flex-col gap-3 p-4 sm:flex-row sm:items-start sm:gap-4">
              <div className="shrink-0">
                <span className="inline-flex h-9 w-9 items-center justify-center border border-phosphor-dim text-[13px] text-phosphor">
                  {s.n}
                </span>
              </div>
              <div className="min-w-0 flex-1">
                <h2 className="text-[13px] uppercase tracking-wide text-ink">
                  {s.title}
                  {s.optional && (
                    <span className="ml-2 text-[10px] uppercase tracking-widest text-amber">
                      optional
                    </span>
                  )}
                </h2>
                <p className="mt-1 text-[12px] leading-relaxed text-muted">{s.body}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {s.links.map((l) => (
                    <Link
                      key={l.to}
                      to={l.to}
                      className="inline-flex items-center gap-1.5 border border-phosphor-dim px-2.5 py-1 text-[12px] uppercase tracking-wide text-phosphor transition-colors hover:border-phosphor hover:bg-phosphor/10"
                    >
                      {l.label} <span className="text-phosphor-dim">→</span>
                    </Link>
                  ))}
                </div>
              </div>
            </div>
          </li>
        ))}
      </ol>

      <p className="text-[11px] text-faint">
        Tip: the sidebar mirrors these steps. Come back here any time from{' '}
        <span className="text-phosphor-dim">00 Guide</span>.
      </p>
    </div>
  )
}
