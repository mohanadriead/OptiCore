import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog'
import { ApiError, errorMessage } from '@/lib/apiClient'
import { useAuth } from '../auth/authContext'
import { PasswordForm } from '../auth/PasswordForm'
import { deactivateEmployee, listEmployees, resetEmployeePassword, setManagerStatus, type Employee } from './employeeApi'
import { EmployeePermissionsDialog } from '../permissions/EmployeePermissionsDialog'

type Action = { employee: Employee; kind: 'deactivate' | 'role' | 'password' }
export function EmployeesPage() {
  const auth = useAuth()
  const client = useQueryClient()
  const result = useQuery({ queryKey: ['employees'], queryFn: ({ signal }) => listEmployees(signal) })
  const [action, setAction] = useState<Action | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [permissionTarget, setPermissionTarget] = useState<Employee | null>(null)
  const { refresh } = auth
  useEffect(() => {
    if (result.error instanceof ApiError && result.error.status === 403) void refresh()
  }, [result.error, refresh])
  const title = action?.kind === 'deactivate' ? 'השבתת עובד' : action?.kind === 'password' ? 'איפוס סיסמה' : action?.employee.isManager ? 'הסרת תפקיד מנהל' : 'מינוי למנהל'
  const completed = async () => {
    setAction(null)
    if (action?.employee.id === auth.employee?.id) await auth.refresh()
    await client.invalidateQueries({ queryKey: ['employees'] })
    toast.success('פרטי העובד עודכנו בהצלחה')
  }
  return <div className="space-y-6">
    <div className="flex items-center justify-between gap-4"><h1>עובדים</h1><Button asChild><Link to="/employees/new">עובד חדש</Link></Button></div>
    {result.isPending && <p role="status">טוען עובדים…</p>}
    {result.isError && <div><p role="alert">{errorMessage(result.error)}</p><Button variant="outline" onClick={() => void result.refetch()}>ניסיון נוסף</Button></div>}
    {result.data && <div className="overflow-x-auto rounded-lg border bg-white"><table className="w-full text-start text-sm"><caption className="sr-only">רשימת עובדים, כולל עובדים לא פעילים</caption>
      <thead><tr>{['מספר עובד', 'שם מלא', 'שם משתמש', 'טלפון', 'תעודת זהות', 'סטטוס', 'תפקיד', 'פעולות'].map(label => <th key={label} className="whitespace-nowrap border-b p-3 text-start">{label}</th>)}</tr></thead>
      <tbody>{result.data.map(employee => <tr key={employee.id} className="border-b last:border-0">
        <td className="p-3"><bdi dir="ltr">{employee.employeeNumber}</bdi></td><td className="p-3"><bdi>{employee.firstName} {employee.lastName}</bdi></td>
        <td className="p-3"><bdi dir="ltr">{employee.username}</bdi></td><td className="p-3"><bdi dir="ltr">{employee.phone}</bdi></td><td className="p-3"><bdi dir="ltr">{employee.nationalId}</bdi></td>
        <td className="whitespace-nowrap p-3">{employee.isActive ? 'פעיל' : 'לא פעיל'}</td><td className="p-3">{employee.isManager ? 'מנהל' : 'עובד'}</td>
        <td className="p-3"><div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setPermissionTarget(employee)}>הרשאות</Button>
          <Button variant="outline" disabled={!employee.isActive} onClick={() => { setError(''); setAction({ employee, kind: 'deactivate' }) }}>השבתה</Button>
          <Button variant="outline" onClick={() => { setError(''); setAction({ employee, kind: 'role' }) }}>{employee.isManager ? 'הסרת ניהול' : 'מינוי למנהל'}</Button>
          {employee.id !== auth.employee?.id && <Button variant="outline" onClick={() => { setError(''); setAction({ employee, kind: 'password' }) }}>איפוס סיסמה</Button>}
        </div></td>
      </tr>)}</tbody>
    </table>{result.data.length === 0 && <p className="p-5">לא נמצאו עובדים.</p>}</div>}
    <Dialog open={!!action} onOpenChange={open => { if (!open && !pending) setAction(null) }}>
      {action && <DialogContent dir="rtl" showCloseButton={!pending}><DialogTitle>{title}</DialogTitle><DialogDescription><bdi>{action.employee.firstName} {action.employee.lastName}</bdi> · מספר עובד <bdi dir="ltr">{action.employee.employeeNumber}</bdi>. {action.kind === 'deactivate' ? 'הרשומה תישאר במערכת, אך העובד לא יוכל להתחבר.' : action.kind === 'role' ? 'האם לאשר את שינוי התפקיד?' : 'הזן סיסמה חדשה עבור העובד.'}</DialogDescription>
        {action.kind === 'password' ? <PasswordForm onBusyChange={setPending} onSave={async (_, next) => {
          try { await resetEmployeePassword(action.employee.employeeNumber, next); await completed() }
          catch (failure) { if (failure instanceof ApiError && failure.status === 403) await auth.refresh(); throw failure }
        }} /> : <>
          {error && <p role="alert" className="text-destructive">{error}</p>}
          <DialogFooter><Button variant="outline" disabled={pending} onClick={() => setAction(null)}>ביטול</Button><Button disabled={pending} onClick={async () => {
            if (pending) return
            setPending(true); setError('')
            try {
              if (action.kind === 'deactivate') await deactivateEmployee(action.employee.employeeNumber)
              else await setManagerStatus(action.employee.employeeNumber, !action.employee.isManager)
              await completed()
            } catch (failure) { setError(errorMessage(failure)); if (failure instanceof ApiError && failure.status === 403) await auth.refresh() }
            finally { setPending(false) }
          }}>{pending ? 'שומר…' : 'אישור'}</Button></DialogFooter>
        </>}
      </DialogContent>}
    </Dialog>
    {permissionTarget && <EmployeePermissionsDialog key={permissionTarget.id} employee={permissionTarget} onClose={() => setPermissionTarget(null)} />}
  </div>
}
