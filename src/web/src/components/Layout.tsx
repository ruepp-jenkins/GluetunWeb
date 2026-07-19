import { useEffect, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../context/AuthContext'
import { Button } from './ui'

const nav = [
  { to: '/settings', label: 'Global Settings', code: '01' },
  { to: '/credentials', label: 'Credentials', code: '02' },
  { to: '/providers', label: 'Providers', code: '03' },
  { to: '/custom-vpn', label: 'Custom VPN', code: '04' },
  { to: '/connections', label: 'Connections', code: '05' },
  { to: '/load-balancers', label: 'Load Balancers', code: '06' },
]

export function Layout() {
  const { status, setStatus } = useAuth()
  const navigate = useNavigate()
  const [docker, setDocker] = useState<{ connected: boolean; endpoint: string } | null>(null)

  // Lightweight Docker connectivity indicator, refreshed periodically.
  useEffect(() => {
    let active = true
    const load = async () => {
      try {
        const s = await api.getSettings()
        if (active) setDocker({ connected: s.dockerConnected, endpoint: s.dockerEndpoint })
      } catch {
        /* ignore */
      }
    }
    void load()
    const t = setInterval(load, 15000)
    return () => {
      active = false
      clearInterval(t)
    }
  }, [])

  async function logout() {
    await api.logout()
    setStatus({ needsSetup: false, authenticated: false, username: null })
    navigate('/')
  }

  return (
    <div className="flex min-h-screen">
      {/* Sidebar */}
      <aside className="flex w-56 shrink-0 flex-col border-r border-line bg-panel">
        <div className="border-b border-line px-4 py-4">
          <div className="text-phosphor">
            <span className="text-muted">~/</span>gluetun<span className="text-phosphor-dim">web</span>
          </div>
          <div className="mt-0.5 text-[10px] uppercase tracking-widest text-faint">
            proxy control panel
          </div>
        </div>

        <nav className="flex-1 py-2">
          {nav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              className={({ isActive }) =>
                `flex items-center gap-2 px-4 py-2 text-[12px] uppercase tracking-wide transition-colors ${
                  isActive
                    ? 'border-l-2 border-phosphor bg-phosphor/5 text-phosphor'
                    : 'border-l-2 border-transparent text-muted hover:bg-panel-2 hover:text-ink'
                }`
              }
            >
              <span className="text-faint">{n.code}</span>
              {n.label}
            </NavLink>
          ))}
        </nav>

        {/* Docker status */}
        <div className="border-t border-line px-4 py-3 text-[11px]">
          <div className="flex items-center gap-2">
            <span
              className={`h-2 w-2 rounded-full ${docker?.connected ? 'bg-phosphor' : 'bg-danger'}`}
            />
            <span className={docker?.connected ? 'text-phosphor' : 'text-danger'}>
              docker {docker?.connected ? 'online' : 'offline'}
            </span>
          </div>
          {docker?.endpoint && (
            <div className="mt-1 truncate text-faint" title={docker.endpoint}>
              {docker.endpoint}
            </div>
          )}
        </div>

        <div className="border-t border-line px-4 py-3">
          <div className="mb-2 text-[11px] text-muted">
            <span className="text-faint">user:</span> {status?.username ?? '—'}
          </div>
          <Button variant="ghost" onClick={logout} className="w-full justify-center">
            logout
          </Button>
        </div>
      </aside>

      {/* Content */}
      <main className="flex-1 overflow-x-hidden">
        <div className="mx-auto max-w-6xl px-6 py-6">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
