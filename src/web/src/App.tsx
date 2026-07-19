import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './context/AuthContext'
import { Layout } from './components/Layout'
import { LoginPage } from './pages/LoginPage'
import { GlobalSettingsPage } from './pages/GlobalSettingsPage'
import { CredentialsPage } from './pages/CredentialsPage'
import { ProvidersPage } from './pages/ProvidersPage'
import { CustomVpnPage } from './pages/CustomVpnPage'
import { ConnectionsPage } from './pages/ConnectionsPage'
import { LoadBalancersPage } from './pages/LoadBalancersPage'
import { Spinner } from './components/ui'

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
        <Route path="/settings" element={<GlobalSettingsPage />} />
        <Route path="/credentials" element={<CredentialsPage />} />
        <Route path="/providers" element={<ProvidersPage />} />
        <Route path="/custom-vpn" element={<CustomVpnPage />} />
        <Route path="/connections" element={<ConnectionsPage />} />
        <Route path="/load-balancers" element={<LoadBalancersPage />} />
        <Route path="/" element={<Navigate to="/connections" replace />} />
        <Route path="*" element={<Navigate to="/connections" replace />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Gate />
      </AuthProvider>
    </BrowserRouter>
  )
}
