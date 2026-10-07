import { Navigate, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { errorMessage } from '@/lib/apiClient'
import { useAuth } from './authContext'

export function SessionLoading() {
  const auth = useAuth()
  return <main dir="rtl" className="mx-auto max-w-lg p-8">{auth.error ? <><p role="alert">{errorMessage(auth.error)}</p><Button onClick={() => void auth.refresh()}>ניסיון נוסף</Button></> : <p role="status">בודק חיבור למערכת…</p>}</main>
}
export function RequireEmployee() {
  const auth = useAuth()
  if (auth.status === 'loading') return <SessionLoading />
  return auth.employee ? <Outlet /> : <Navigate to="/login" replace />
}
export function RequireManager() {
  return useAuth().employee?.isManager ? <Outlet /> : <Navigate to="/customers" replace />
}
