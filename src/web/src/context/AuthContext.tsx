import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import type { AuthStatus } from '../api/types'

interface AuthCtx {
  status: AuthStatus | null
  loading: boolean
  refresh: () => Promise<void>
  setStatus: (s: AuthStatus) => void
}

const Ctx = createContext<AuthCtx | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus | null>(null)
  const [loading, setLoading] = useState(true)

  async function refresh() {
    try {
      setStatus(await api.authStatus())
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void refresh()
  }, [])

  return <Ctx.Provider value={{ status, loading, refresh, setStatus }}>{children}</Ctx.Provider>
}

export function useAuth() {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
