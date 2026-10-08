import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog'
import { ApiError, errorMessage } from '@/lib/apiClient'
import { useAuth } from '../auth/authContext'
import type { Employee } from '../employees/employeeApi'
import { getEmployeePermissions, getPermissionCatalog, permissionKeys, setEmployeePermissions, type PermissionCode } from './permissionApi'

export function EmployeePermissionsDialog({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const auth = useAuth()
  const allowed = auth.status === 'authenticated' && !!auth.employee?.isManager
  const client = useQueryClient()
  const catalog = useQuery({ queryKey: permissionKeys.catalog, queryFn: ({ signal }) => getPermissionCatalog(signal), enabled: allowed })
  const target = useQuery({ queryKey: permissionKeys.employee(employee.employeeNumber), queryFn: ({ signal }) => getEmployeePermissions(employee.employeeNumber, signal), enabled: allowed, staleTime: 0 })
  const [edited, setEdited] = useState<PermissionCode[] | null>(null)
  const [pending, setPending] = useState(false)
  const [saveError, setSaveError] = useState('')
  const queryError = catalog.error || target.error
  const { refresh } = auth
  useEffect(() => { if (queryError instanceof ApiError && queryError.status === 403) void refresh() }, [queryError, refresh])
  if (!allowed) return null
  const selected = edited ?? target.data?.assignedPermissions ?? []
  return <Dialog open onOpenChange={open => { if (!open && !pending) onClose() }}>
    <DialogContent dir="rtl" showCloseButton={!pending}>
      <DialogTitle>הרשאות עובד</DialogTitle>
      <DialogDescription><bdi>{employee.firstName} {employee.lastName}</bdi> · מספר עובד <bdi dir="ltr">{employee.employeeNumber}</bdi></DialogDescription>
      {queryError ? <div><p role="alert">{errorMessage(queryError)}</p><Button variant="outline" onClick={() => { void catalog.refetch(); void target.refetch() }}>ניסיון נוסף</Button></div>
        : catalog.isPending || target.isPending ? <p role="status">טוען הרשאות…</p>
        : <form className="space-y-4" onSubmit={async event => {
          event.preventDefault()
          if (pending || target.data.isManager) return
          setPending(true); setSaveError('')
          try {
            const updated = await setEmployeePermissions(employee.employeeNumber, selected)
            client.setQueryData(permissionKeys.employee(employee.employeeNumber), updated)
            setEdited(null)
            await Promise.all([
              client.invalidateQueries({ queryKey: permissionKeys.employee(employee.employeeNumber) }),
              client.invalidateQueries({ queryKey: permissionKeys.effective }),
            ])
            toast.success('הרשאות העובד עודכנו בהצלחה')
          } catch (failure) {
            setSaveError(errorMessage(failure))
            if (failure instanceof ApiError && failure.status === 403) await auth.refresh()
          } finally { setPending(false) }
        }}>
          {target.data.isManager && <p className="rounded border bg-accent p-3 text-sm">למנהלים יש את כל ההרשאות באופן אוטומטי</p>}
          <fieldset disabled={pending || target.data.isManager} className="space-y-3">
            <legend className="sr-only">הרשאות הניתנות להגדרה</legend>
            {catalog.data.map(permission => <label key={permission.code} className="flex items-center gap-3 text-sm">
              <input type="checkbox" className="size-4 accent-primary" checked={target.data.isManager || selected.includes(permission.code)} onChange={event => {
                setEdited(event.target.checked ? [...selected, permission.code] : selected.filter(code => code !== permission.code))
              }} />{permission.label}
            </label>)}
          </fieldset>
          {saveError && <p role="alert" className="text-destructive">{saveError}</p>}
          <DialogFooter><Button type="button" variant="outline" disabled={pending} onClick={onClose}>סיום</Button>
            {!target.data.isManager && <Button type="submit" disabled={pending}>{pending ? 'שומר…' : 'שמירת הרשאות'}</Button>}
          </DialogFooter>
        </form>}
    </DialogContent>
  </Dialog>
}
