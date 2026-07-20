import { useEffect, useState } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './context/AuthContext'
import { ButtonStyleProvider } from './context/ButtonStyleContext'
import { Layout } from './components/Layout'
import { LoginPage } from './pages/LoginPage'
import { GuidePage } from './pages/GuidePage'
import { GlobalSettingsPage } from './pages/GlobalSettingsPage'
import { CredentialsPage } from './pages/CredentialsPage'
import { ProvidersPage } from './pages/ProvidersPage'
import { CustomVpnPage } from './pages/CustomVpnPage'
import { ConnectionsPage } from './pages/ConnectionsPage'
import { LoadBalancersPage } from './pages/LoadBalancersPage'
import { api } from './api/client'
import { Spinner } from './components/ui'

/**
 * Landing decision for "/": a fresh install with no connections starts on the guide, otherwise it
 * goes straight to the connections list. Connection state (running or not) doesn't matter — only
 * whether any exist. Decided once on load; the user can navigate freely afterwards.
 */
function DefaultLanding() {
  const [target, setTarget] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    api
      .listConnections()
      .then((c) => active && setTarget(c.length > 0 ? '/connections' : '/guide'))
      // If the check fails, the guide is the safe, informative fallback.
      .catch(() => active && setTarget('/guide'))
    return () => {
      active = false
    }
  }, [])

  if (target === null) return <Spinner label="loading…" />
  return <Navigate to={target} replace />
}

function Gate() {
  const { status, loading } = useAuth()

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Spinner label="booting…" />
      </div>
    )
  }

  if (!status?.authenticated) return <LoginPage />

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/guide" element={<GuidePage />} />
        <Route path="/settings" element={<GlobalSettingsPage />} />
        <Route path="/credentials" element={<CredentialsPage />} />
        <Route path="/providers" element={<ProvidersPage />} />
        <Route path="/custom-vpn" element={<CustomVpnPage />} />
        <Route path="/connections" element={<ConnectionsPage />} />
        <Route path="/load-balancers" element={<LoadBalancersPage />} />
        <Route path="/" element={<DefaultLanding />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ButtonStyleProvider>
          <Gate />
        </ButtonStyleProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
