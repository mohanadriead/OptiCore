import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog'
import { ApiError, errorMessage } from '@/lib/apiClient'
import { useAuth } from '@/features/auth/authContext'
import { correctAttendance, getAttendanceDetails, getAttendanceHistory, type AttendanceDetails, type AttendanceFilters } from './attendanceApi'
import { attendanceTime, israelInput, israelUtc, validEmployeeNumber } from './attendanceTime'

export function AttendanceManagementPage() {
  const { refresh } = useAuth()
  const [draft, setDraft] = useState<AttendanceFilters>({ employeeNumber: '', from: '', to: '' })
  const [filters, setFilters] = useState(draft)
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)
  const [validation, setValidation] = useState('')
  const [saved, setSaved] = useState(false)
  const history = useQuery({ queryKey: ['attendance', 'history', filters, page], retry: false,
    queryFn: ({ signal }) => getAttendanceHistory(filters, page, signal) })
  const details = useQuery({ queryKey: ['attendance', 'details', selected], enabled: !!selected, retry: false,
    queryFn: ({ signal }) => getAttendanceDetails(selected!, signal) })
  const error = history.error ?? details.error
  useEffect(() => { if (error instanceof ApiError && error.status === 403) void refresh() }, [error, refresh])
  function search(event: FormEvent) {
    event.preventDefault()
    if ((draft.employeeNumber && !validEmployeeNumber(draft.employeeNumber)) || (draft.from && draft.to && draft.from > draft.to)) {
      setValidation('יש להזין מספר עובד תקין וטווח תאריכים תקין.'); return
    }
    setValidation(''); setFilters({ ...draft }); setPage(1); setSelected(null); setSaved(false)
  }
  return <div dir="rtl" className="space-y-5">
    <h1>ניהול נוכחות</h1>
    <form onSubmit={search} className="flex flex-wrap items-end gap-3 rounded-lg border bg-white p-4">
      <label>מספר עובד<Input dir="ltr" inputMode="numeric" value={draft.employeeNumber} onChange={event => setDraft({ ...draft, employeeNumber: event.target.value })} /></label>
      <label>מתאריך<Input type="date" value={draft.from} onChange={event => setDraft({ ...draft, from: event.target.value })} /></label>
      <label>עד תאריך<Input type="date" value={draft.to} onChange={event => setDraft({ ...draft, to: event.target.value })} /></label>
      <Button type="submit">סינון</Button>
      <p className="w-full text-sm">התאריכים כוללים את יום ההתחלה ויום הסיום, לפי תאריך הכניסה בשעון ישראל. לסינון יום אחד בחרו אותו תאריך בשני השדות.</p>
    </form>
    {validation && <p role="alert">{validation}</p>}
    {saved && <p role="status">התיקון נשמר בהצלחה.</p>}
    {history.isPending && <p role="status">טוען היסטוריית נוכחות…</p>}
    {history.error && <><p role="alert">{errorMessage(history.error)}</p><Button onClick={() => void history.refetch()}>ניסיון נוסף</Button></>}
    {history.data && <>
      {history.data.items.length === 0 ? <p>לא נמצאו רשומות נוכחות.</p> : <div className="overflow-x-auto"><table className="w-full text-start">
        <caption className="text-start">היסטוריית נוכחות</caption>
        <thead><tr>{['עובד', 'כניסה', 'יציאה', 'סוג יציאה', 'פרטים'].map(label => <th key={label} className="p-2 text-start">{label}</th>)}</tr></thead>
        <tbody>{history.data.items.map(row => <tr key={row.id} className="border-t">
          <td className="p-2">{row.firstName} {row.lastName} ({row.employeeNumber})</td>
          <td className="p-2">{attendanceTime(row.checkInAtUtc)}</td><td className="p-2">{attendanceTime(row.checkOutAtUtc)}</td>
          <td className="p-2">{row.wasCheckoutAutomatic ? 'אוטומטית בחצות' : row.checkOutAtUtc ? 'ידנית' : 'פתוחה'}</td>
          <td className="p-2"><div className="flex gap-2">
            <Button onClick={() => { setSelected(row.id); setEditing(true); setSaved(false) }} aria-label={`עריכה של ${row.firstName} ${row.lastName}`}>עריכה</Button>
            <Button variant="outline" onClick={() => { setSelected(row.id); setEditing(false); setSaved(false) }} aria-label={`פתיחת רשומה של ${row.firstName} ${row.lastName}`}>פתיחה</Button>
          </div></td>
        </tr>)}</tbody>
      </table></div>}
      <div className="flex items-center gap-3"><Button disabled={page === 1 || history.isFetching} onClick={() => { setPage(page - 1); setSelected(null); setSaved(false) }}>הקודם</Button>
        <p>עמוד {page} · {history.data.total} רשומות</p>
        <Button disabled={page * history.data.pageSize >= history.data.total || history.isFetching} onClick={() => { setPage(page + 1); setSelected(null); setSaved(false) }}>הבא</Button></div>
    </>}
    {selected && <section aria-label="פרטי רשומה" className="space-y-4 rounded-lg border bg-white p-5">
      <Button variant="outline" onClick={() => { setSelected(null); setSaved(false) }}>סגירת פרטים</Button>
      {details.isPending && <p role="status">טוען פרטי רשומה…</p>}
      {details.error && <><p role="alert">{errorMessage(details.error)}</p><Button onClick={() => void details.refetch()}>טעינה מחדש</Button></>}
      {details.data && <CorrectionForm key={`${details.data.record.id}:${details.data.record.updatedAtUtc}:${editing}`} details={details.data}
        editing={editing} onCancel={() => setEditing(false)} onEdit={() => { setEditing(true); setSaved(false) }} onSaved={() => { setSaved(true); setEditing(false) }} />}
    </section>}
  </div>
}

function CorrectionForm({ details, editing, onCancel, onEdit, onSaved }: {
  details: AttendanceDetails; editing: boolean; onCancel: () => void; onEdit: () => void; onSaved: () => void
}) {
  const { record, corrections } = details
  const auth = useAuth()
  const client = useQueryClient()
  const [checkIn, setCheckIn] = useState(israelInput(record.checkInAtUtc).slice(0, 16))
  const [checkOut, setCheckOut] = useState(israelInput(record.checkOutAtUtc).slice(0, 16))
  const [confirmation, setConfirmation] = useState<{ start: string; end: string | null; reason: string } | null>(null)
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const request = useRef<AbortController | null>(null)
  useEffect(() => () => request.current?.abort(), [])
  function submit(event: FormEvent) {
    event.preventDefault()
    if (request.current) return
    setError('')
    if (!reason.trim() || reason.trim().length > 2000) { setError('סיבת התיקון נדרשת ועד 2000 תווים.'); return }
    if (!/^\d{4}-\d{2}-\d{2}T([01]\d|2[0-3]):[0-5]\d$/.test(checkIn) ||
      (checkOut && !/^\d{4}-\d{2}-\d{2}T([01]\d|2[0-3]):[0-5]\d$/.test(checkOut))) {
      setError('יש להזין תאריך ושעה תקינים בפורמט HH:mm לפי שעון ישראל.'); return
    }
    let start: string | null, end: string | null
    try { start = israelUtc(checkIn, record.checkInAtUtc); end = israelUtc(checkOut, record.checkOutAtUtc) }
    catch { setError('יש להזין זמן תקין לפי שעון ישראל.'); return }
    if (!start || (end && new Date(end) < new Date(start))) { setError('היציאה חייבת להיות לאחר הכניסה.'); return }
    setConfirmation({ start, end, reason: reason.trim() })
  }
  async function save() {
    if (!confirmation || request.current) return
    const controller = new AbortController(); request.current = controller; setPending(true)
    try {
      const updated = await correctAttendance(record, confirmation.start, confirmation.end, confirmation.reason, controller.signal)
      if (controller.signal.aborted) return
      client.setQueryData(['attendance', 'details', record.id], updated)
      void client.invalidateQueries({ queryKey: ['attendance', 'history'] })
      void client.invalidateQueries({ queryKey: ['attendance', 'details', record.id] })
      onSaved()
    } catch (failure) {
      if (controller.signal.aborted || (failure instanceof Error && failure.name === 'AbortError')) return
      setError(errorMessage(failure))
      setConfirmation(null)
      if (failure instanceof ApiError && failure.status === 403) await auth.refresh()
    } finally { if (!controller.signal.aborted) { request.current = null; setPending(false) } }
  }
  return <>
    <h2>פרטי נוכחות — {record.firstName} {record.lastName} ({record.employeeNumber})</h2>
    <dl className="grid gap-2 sm:grid-cols-2">
      <dt>כניסה</dt><dd>{attendanceTime(record.checkInAtUtc)}</dd><dt>יציאה</dt><dd>{attendanceTime(record.checkOutAtUtc)}</dd>
      <dt>סוג יציאה</dt><dd>{record.wasCheckoutAutomatic ? 'אוטומטית בחצות' : record.checkOutAtUtc ? 'ידנית' : 'פתוחה'}</dd>
      <dt>גבול חצות</dt><dd>{attendanceTime(record.automaticCheckoutDueAtUtc)}</dd>
      <dt>זמן עיבוד יציאה</dt><dd>{attendanceTime(record.checkoutProcessedAtUtc)}</dd>
    </dl>
    {!editing && <Button onClick={onEdit}>עריכה</Button>}
    {editing && <form onSubmit={submit} noValidate aria-label="עריכת נוכחות" aria-busy={pending} className="space-y-3">
      <h3>תיקון נוכחות</h3><p>הזמנים לפי שעון ישראל. בשעה חוזרת במעבר לשעון חורף, זמן חדש מתייחס למופע הראשון.</p>
      <AttendanceTimeFields label="כניסה" value={checkIn} disabled={pending} onChange={setCheckIn} />
      <AttendanceTimeFields label="יציאה" value={checkOut} disabled={pending} onChange={setCheckOut} />
      <p>להוספת יציאה חסרה מלאו את זמן היציאה. לא ניתן לפתוח מחדש רשומה סגורה או לקבוע יציאה לאחר חצות.</p>
      <label className="block">סיבת התיקון<Textarea required maxLength={2000} disabled={pending} value={reason} onChange={event => setReason(event.target.value)} /></label>
      <Button type="submit" disabled={pending}>שמירת שינויים</Button>
      <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>ביטול</Button>
      <Button type="button" variant="outline" disabled={pending} onClick={() => void client.invalidateQueries({ queryKey: ['attendance', 'details', record.id] })}>טעינה מחדש</Button>
      {pending && <p role="status">שומר תיקון…</p>}{error && <p role="alert">{error}</p>}
    </form>}
    <Dialog open={confirmation !== null} onOpenChange={open => { if (!open && !pending) setConfirmation(null) }}>
      <DialogContent dir="rtl" showCloseButton={!pending} onEscapeKeyDown={event => { if (pending) event.preventDefault() }} onPointerDownOutside={event => { if (pending) event.preventDefault() }}>
        <DialogHeader><DialogTitle>אישור שמירת שינויים</DialogTitle>
          <DialogDescription>השינויים יישמרו עם סיבת התיקון בהיסטוריית התיקונים של {record.firstName} {record.lastName}.</DialogDescription></DialogHeader>
        {confirmation && <div className="space-y-2">
          <p>שעת כניסה: {attendanceTime(confirmation.start)}</p>
          <p>שעת יציאה: {attendanceTime(confirmation.end)}</p>
          <p className="whitespace-pre-wrap">סיבת התיקון: {confirmation.reason}</p>
        </div>}
        <DialogFooter><Button disabled={pending} onClick={() => void save()}>אישור ושמירה</Button>
          <Button variant="outline" disabled={pending} onClick={() => setConfirmation(null)}>ביטול</Button></DialogFooter>
        {pending && <p role="status">שומר תיקון…</p>}
      </DialogContent>
    </Dialog>
    <h3>היסטוריית תיקונים</h3>
    {corrections.length === 0 ? <p>אין תיקונים לרשומה זו.</p> : <ol className="space-y-3">{corrections.map((correction, index) => <li key={index} className="rounded border p-3">
      <p>{correction.correctedByFirstName} {correction.correctedByLastName} ({correction.correctedByEmployeeNumber}) · {attendanceTime(correction.correctedAtUtc)}</p>
      <p>סיבה: {correction.reason}</p>
      <p>כניסה קודמת: {attendanceTime(correction.previousCheckInAtUtc)} · כניסה חדשה: {attendanceTime(correction.newCheckInAtUtc)}</p>
      <p>יציאה קודמת: {attendanceTime(correction.previousCheckOutAtUtc)} · יציאה חדשה: {attendanceTime(correction.newCheckOutAtUtc)}</p>
    </li>)}</ol>}
  </>
}

function AttendanceTimeFields({ label, value, disabled, onChange }: {
  label: string; value: string; disabled: boolean; onChange: (value: string) => void
}) {
  const [date = '', time = ''] = value.split('T')
  return <div className="grid gap-3 sm:grid-cols-2">
    <label>תאריך {label}<Input type="date" dir="ltr" autoFocus={label === 'כניסה'} disabled={disabled} value={date}
      onChange={event => onChange(event.target.value || time ? `${event.target.value}T${time}` : '')} /></label>
    <label>שעת {label}<Input type="text" dir="ltr" inputMode="numeric" placeholder="HH:mm" maxLength={5} disabled={disabled} value={time}
      onChange={event => onChange(date || event.target.value ? `${date}T${event.target.value}` : '')} /></label>
  </div>
}
