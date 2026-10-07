import { useState } from 'react'
import { Navigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { errorMessage } from '@/lib/apiClient'
import { useAuth } from './authContext'
import { SessionLoading } from './RouteGuards'

export function LoginPage() {
  const auth = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  if (auth.status === 'loading') return <SessionLoading />
  if (auth.employee) return <Navigate to="/customers" replace />
  return <main dir="rtl" className="mx-auto flex min-h-screen max-w-md flex-col justify-center p-6">
    <form className="space-y-5 rounded-lg border bg-white p-6 shadow-sm" onSubmit={async event => {
      event.preventDefault()
      if (pending) return
      if (!username.trim() || !password) { setError('יש להזין שם משתמש וסיסמה.'); return }
      setPending(true); setError('')
      try { await auth.signIn(username.trim(), password) } catch (failure) { setError(errorMessage(failure)) }
      finally { setPassword(''); setPending(false) }
    }}>
      <p className="text-xl font-semibold text-primary">OptiCore</p><h1>כניסה למערכת</h1>
      <label className="block space-y-2">שם משתמש<Input name="username" autoComplete="username" dir="ltr" value={username} onChange={event => setUsername(event.target.value)} required disabled={pending} /></label>
      <label className="block space-y-2">סיסמה<Input name="password" type="password" autoComplete="current-password" dir="ltr" value={password} onChange={event => setPassword(event.target.value)} required disabled={pending} /></label>
      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      <Button type="submit" disabled={pending} className="w-full">{pending ? 'מתחבר…' : 'כניסה'}</Button>
    </form>
  </main>
}
