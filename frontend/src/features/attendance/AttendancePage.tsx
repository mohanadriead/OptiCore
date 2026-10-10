import { useEffect, useRef, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ApiError, errorMessage } from '@/lib/apiClient'
import { useAuth } from '@/features/auth/authContext'
import { getAttendanceStatus, getMyAttendanceStatus, submitAttendance, submitMyAttendance, type AttendanceAction, type AttendanceStatus } from './attendanceApi'
import { attendanceTime, validEmployeeNumber } from './attendanceTime'

export function AttendancePage() {
  const auth = useAuth()
  return <AttendanceWorkflow key={`${auth.employee?.id}:${auth.employee?.isManager}`} isManager={auth.employee?.isManager === true} />
}

function AttendanceWorkflow({ isManager }: { isManager: boolean }) {
  const auth = useAuth()
  const [employeeNumber, setEmployeeNumber] = useState('')
  const [employee, setEmployee] = useState<AttendanceStatus | null>(null)
  const [feedback, setFeedback] = useState<{ message: string; error: boolean } | null>(null)
  const [pending, setPending] = useState<'lookup' | AttendanceAction | null>(null)
  const request = useRef<AbortController | null>(null)
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (!isManager) void run('lookup')
    return () => { request.current?.abort(); request.current = null }
    // A role/identity change remounts this workflow; status is fetched once per mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isManager])

  async function run(action: 'lookup' | AttendanceAction) {
    if (request.current) return
    setFeedback(null)
    if (isManager && !validEmployeeNumber(employeeNumber)) {
      setFeedback({ message: 'יש להזין מספר עובד שהוא מספר שלם חיובי עד 2147483647.', error: true })
      input.current?.focus()
      return
    }
    if (action !== 'lookup' && (!employee || (action === 'check-in') === employee.hasOpenAttendance)) return
    if (action === 'lookup') setEmployee(null)
    const controller = new AbortController()
    request.current = controller
    setPending(action)
    try {
      const number = Number(employeeNumber)
      if (action !== 'lookup') {
        const result = isManager ? await submitAttendance(number, action, controller.signal) : await submitMyAttendance(action, controller.signal)
        if (controller.signal.aborted) return
        if (result.employeeNumber !== employee!.employeeNumber) throw new ApiError(502, 'פרטי הנוכחות שהתקבלו אינם תקינים.')
        setEmployee({ ...employee!, hasOpenAttendance: result.checkOutAtUtc === null,
          checkInAtUtc: result.checkOutAtUtc === null ? result.checkInAtUtc : null })
        const message = `${action === 'check-out' ? 'היציאה' : 'הכניסה'} נרשמה בהצלחה עבור ${employee!.firstName} ${employee!.lastName}.`
        setFeedback({ message, error: false })
        toast.success(message)
      } else {
        const status = isManager ? await getAttendanceStatus(number, controller.signal) : await getMyAttendanceStatus(controller.signal)
        if (!controller.signal.aborted) setEmployee(status)
      }
    } catch (failure) {
      if (controller.signal.aborted || (failure instanceof Error && failure.name === 'AbortError')) return
      setFeedback({ message: errorMessage(failure), error: true })
      if (failure instanceof ApiError && (failure.status === 409 || failure.status === 502)) setEmployee(null)
      if (failure instanceof ApiError && failure.status === 403) await auth.refresh()
    } finally {
      if (!controller.signal.aborted) { request.current = null; setPending(null) }
    }
  }

  function lookup(event: FormEvent) { event.preventDefault(); void run('lookup') }
  return <div dir="rtl" className="max-w-xl space-y-5">
    <h1>נוכחות</h1>
    <form noValidate onSubmit={lookup} aria-busy={pending !== null} className="space-y-5 rounded-lg border bg-white p-5">
      {isManager ? <>
      <p>הזינו מספר עובד כדי לראות את שמו ואת מצב הנוכחות לפני רישום כניסה או יציאה.</p>
      <label htmlFor="attendance-employee-number">מספר עובד</label>
      <Input ref={input} id="attendance-employee-number" type="text" dir="ltr" inputMode="numeric" autoComplete="off"
        value={employeeNumber} disabled={pending !== null} onChange={event => { setEmployeeNumber(event.target.value); setEmployee(null); setFeedback(null) }} />
      <Button type="submit" disabled={pending !== null}>חיפוש עובד</Button>
      </> : <>
        <p>הנוכחות שלי</p>
        <Button type="button" disabled={pending !== null} onClick={() => void run('lookup')}>רענון נוכחות</Button>
      </>}
      {employee && <section aria-label="מצב נוכחות" className="space-y-3">
        <h2>{employee.firstName} {employee.lastName}</h2>
        <p>מספר עובד: {employee.employeeNumber}</p>
        <p>{employee.hasOpenAttendance ? `נוכחות פתוחה — כניסה: ${attendanceTime(employee.checkInAtUtc)}` : 'אין נוכחות פתוחה'}</p>
        <div className="flex flex-col gap-3 sm:flex-row">
          <Button type="button" disabled={pending !== null || employee.hasOpenAttendance} onClick={() => void run('check-in')} className="h-11 flex-1">כניסה</Button>
          <Button type="button" variant="outline" disabled={pending !== null || !employee.hasOpenAttendance} onClick={() => void run('check-out')} className="h-11 flex-1">יציאה</Button>
        </div>
      </section>}
      {pending && <p role="status">{pending === 'lookup' ? (isManager ? 'מחפש עובד…' : 'טוען את הנוכחות שלי…') : pending === 'check-in' ? 'רושם כניסה…' : 'רושם יציאה…'}</p>}
      {feedback && <p role={feedback.error ? 'alert' : 'status'} className={feedback.error ? 'text-destructive' : 'text-primary'}>{feedback.message}</p>}
    </form>
  </div>
}
