import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { errorMessage } from '@/lib/apiClient'
import { useAuth } from './authContext'

export function CurrentEmployeeMenu() {
  const auth = useAuth()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  return <div className="flex flex-wrap items-center gap-3 text-sm">
    <span><bdi>{auth.employee?.firstName} {auth.employee?.lastName}</bdi>{auth.employee?.isManager && <span className="ms-2 rounded bg-accent px-2 py-1">מנהל</span>}</span>
    <Link to="/change-password" className="text-primary">שינוי סיסמה</Link>
    <Button variant="outline" disabled={pending} onClick={async () => {
      if (pending) return
      setPending(true); setError('')
      try { await auth.signOut() } catch (failure) { setError(errorMessage(failure)) } finally { setPending(false) }
    }}>{pending ? 'מתנתק…' : 'התנתקות'}</Button>
    {error && <p role="alert" className="text-destructive">{error}</p>}
  </div>
}
