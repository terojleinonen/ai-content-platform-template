import { useEffect, useState, type FormEvent } from 'react'
import { api } from '../api'
import type { CurrentUser } from '../types'

const PROVIDER_LABELS: Record<string, string> = { Google: 'Google', Microsoft: 'Microsoft' }

/** Reads (and removes from the address bar) an error sent back by the external sign-in flow. */
function takeAuthErrorFromUrl() {
  const params = new URLSearchParams(window.location.search)
  const error = params.get('authError')
  if (error) window.history.replaceState(null, '', window.location.pathname + window.location.hash)
  return error ?? undefined
}

export function LoginPage({ onSignedIn }: { onSignedIn: (user: CurrentUser) => void }) {
  const [mode, setMode] = useState<'signin' | 'register'>('signin')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | undefined>(takeAuthErrorFromUrl)
  const [providers, setProviders] = useState<string[]>([])

  useEffect(() => {
    api.providers().then(setProviders, () => {})
  }, [])

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(undefined)
    try {
      if (mode === 'register') await api.register(email, password)
      await api.login(email, password)
      onSignedIn(await api.me())
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const registering = mode === 'register'

  return (
    <div className="login">
      <form className="card form login-card" onSubmit={submit}>
        <div>
          <div className="brand login-brand">
            <span className="logo">✍️</span> AI Content Platform
          </div>
          <h2>{registering ? 'Create your account' : 'Sign in'}</h2>
        </div>

        {providers.length > 0 && (
          <>
            <div className="login-providers">
              {providers.map((p) => (
                <a key={p} className="button ghost" href={`/api/auth/external/${p}?returnUrl=/`}>
                  Continue with {PROVIDER_LABELS[p] ?? p}
                </a>
              ))}
            </div>
            <div className="divider">
              <span>or with email</span>
            </div>
          </>
        )}

        <label>
          Email
          <input type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>
        <label>
          Password
          <input
            type="password"
            autoComplete={registering ? 'new-password' : 'current-password'}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          {registering && (
            <span className="muted small">At least 8 characters, with upper- and lowercase letters, a number and a symbol.</span>
          )}
        </label>

        <button className="primary" disabled={busy || !email || !password}>
          {busy ? <span className="spinner" /> : null} {registering ? 'Create account' : 'Sign in'}
        </button>
        {error && <p className="error">{error}</p>}

        <p className="muted small login-switch">
          {registering ? 'Already have an account?' : 'New here?'}{' '}
          <button
            type="button"
            className="link"
            onClick={() => {
              setMode(registering ? 'signin' : 'register')
              setError(undefined)
            }}
          >
            {registering ? 'Sign in' : 'Create an account'}
          </button>
        </p>
      </form>
    </div>
  )
}
