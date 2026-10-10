import { useEffect, useRef, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ApiError, errorMessage } from '@/lib/apiClient'
import { useAuth } from '@/features/auth/authContext'
import { submitAttendance, type AttendanceAction } from './attendanceApi'

const validationMessage = 'יש להזין מספר עובד שהוא מספר שלם חיובי עד 2147483647.'

export function AttendancePage() {
  const auth = useAuth()
  const [employeeNumber, setEmployeeNumber] = useState('')
  const [validation, setValidation] = useState('')
  const [feedback, setFeedback] = useState<{ message: string; error: boolean } | null>(null)
  const [pending, setPending] = useState<AttendanceAction | null>(null)
  const request = useRef<AbortController | null>(null)
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => () => { request.current?.abort() }, [])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (request.current) return
    // Enter has no implicit action: a manager must choose entry or exit explicitly.
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    const action = submitter instanceof HTMLButtonElement ? submitter.value : ''
    if (action !== 'check-in' && action !== 'check-out') return
    setFeedback(null)
    const number = Number(employeeNumber)
    if (!/^[0-9]+$/.test(employeeNumber) || !Number.isInteger(number) || number < 1 || number > 2147483647) {
      setValidation(validationMessage)
      input.current?.focus()
      return
    }
    setValidation('')
    const controller = new AbortController()
    request.current = controller
    setPending(action)
    try {
      await submitAttendance(number, action, controller.signal)
      if (controller.signal.aborted) return
      const message = `${action === 'check-in' ? 'הכניסה' : 'היציאה'} נרשמה בהצלחה עבור עובד מספר ${number}.`
      setFeedback({ message, error: false })
      toast.success(message)
    } catch (failure) {
      if (controller.signal.aborted || (failure instanceof Error && failure.name === 'AbortError')) return
      setFeedback({ message: errorMessage(failure), error: true })
      if (failure instanceof ApiError && failure.status === 403) await auth.refresh()
    } finally {
      if (!controller.signal.aborted) {
        request.current = null
        setPending(null)
      }
    }
  }

  return <div dir="rtl" className="max-w-xl space-y-5">
    <h1>נוכחות</h1>
    <form noValidate onSubmit={submit} aria-busy={pending !== null} className="space-y-5 rounded-lg border bg-white p-5">
      <p id="attendance-help" className="text-sm text-muted-foreground">הזינו מספר עובד ובחרו כניסה או יציאה.</p>
      <div className="space-y-2">
        <label htmlFor="attendance-employee-number">מספר עובד <span aria-hidden="true" className="text-destructive">*</span></label>
        <Input ref={input} id="attendance-employee-number" type="text" dir="ltr" inputMode="numeric" autoComplete="off" required
          value={employeeNumber} disabled={pending !== null} aria-invalid={!!validation}
          aria-describedby={`attendance-help${validation ? ' attendance-validation' : ''}`}
          onKeyDown={event => { if (event.key === 'Enter') event.preventDefault() }}
          onChange={event => { setEmployeeNumber(event.target.value); setValidation(''); setFeedback(null) }} />
        {validation && <p id="attendance-validation" role="alert" className="text-sm text-destructive">{validation}</p>}
      </div>
      <div className="flex flex-col gap-3 sm:flex-row">
        <Button type="submit" value="check-in" disabled={pending !== null} className="h-11 flex-1">כניסה</Button>
        <Button type="submit" value="check-out" variant="outline" disabled={pending !== null} className="h-11 flex-1">יציאה</Button>
      </div>
      {pending && <p role="status" className="text-sm text-muted-foreground">{pending === 'check-in' ? 'רושם כניסה…' : 'רושם יציאה…'}</p>}
      {feedback && <p role={feedback.error ? 'alert' : 'status'} className={feedback.error ? 'text-destructive' : 'text-primary'}>{feedback.message}</p>}
    </form>
  </div>
}
