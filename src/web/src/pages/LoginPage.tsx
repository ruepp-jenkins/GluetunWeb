import { useState } from 'react'
import { api, ApiError } from '../api/client'
import { useAuth } from '../context/AuthContext'
import { Field, Input } from '../components/Field'
import { Button, Banner } from '../components/ui'

export function LoginPage() {
  const { status, setStatus } = useAuth()
  const isSetup = status?.needsSetup ?? false

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const fieldErrors: Record<string, string> = {}
  if (isSetup) {
    if (password && password.length < 8) fieldErrors.password = 'At least 8 characters.'
    if (confirm && confirm !== password) fieldErrors.confirm = 'Passwords do not match.'
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (isSetup && (password.length < 8 || password !== confirm)) return
    setBusy(true)
    try {
      const next = isSetup ? await api.setup(username, password) : await api.login(username, password)
      setStatus(next)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Request failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <div className="w-full max-w-md border border-line bg-panel">
        <header className="border-b border-line bg-panel-2 px-4 py-3">
          <div className="text-phosphor">
            <span className="text-muted">~/</span>gluetun<span className="text-phosphor-dim">web</span>
          </div>
          <div className="mt-0.5 text-[11px] uppercase tracking-widest text-faint">
            {isSetup ? 'initialize administrator' : 'authenticate'}
          </div>
        </header>

        <form onSubmit={submit} className="space-y-4 p-4">
          {isSetup && (
            <Banner kind="info">
              First run — create the administrator account. The password is hashed (PBKDF2) and stored
              server-side only.
            </Banner>
          )}
          {error && <Banner kind="error">{error}</Banner>}

          <Field label="Username" required>
            <Input
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              placeholder={isSetup ? 'choose a username' : 'username'}
              autoFocus
            />
          </Field>

          <Field label="Password" required error={fieldErrors.password}>
            <Input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete={isSetup ? 'new-password' : 'current-password'}
              error={fieldErrors.password}
            />
          </Field>

          {isSetup && (
            <Field label="Confirm password" required error={fieldErrors.confirm}>
              <Input
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                autoComplete="new-password"
                error={fieldErrors.confirm}
              />
            </Field>
          )}

          <Button variant="primary" type="submit" disabled={busy} className="w-full justify-center">
            {busy ? '···' : isSetup ? 'create & sign in' : 'sign in'}
          </Button>
        </form>
      </div>
    </div>
  )
}
