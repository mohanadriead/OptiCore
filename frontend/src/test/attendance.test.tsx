import { beforeEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { toast } from 'sonner'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { routes } from '@/app/router'
import type { Employee } from '@/features/employees/employeeApi'
import type { AttendanceDetails, AttendanceRecord, AttendanceStatus } from '@/features/attendance/attendanceApi'

const manager: Employee = { id: 'manager-test', employeeNumber: 1, firstName: 'Test', lastName: 'Manager', username: 'manager-test', phone: '0000000000', nationalId: '000000000', isActive: true, isManager: true, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: null }
const record: AttendanceRecord = { id: '00000000-0000-0000-0000-000000000042', employeeNumber: 42, firstName: 'דנה', lastName: 'כהן', checkInAtUtc: '2026-07-01T10:00:00Z', checkOutAtUtc: null, wasCheckoutAutomatic: false, automaticCheckoutDueAtUtc: '2026-07-01T21:00:00Z', checkoutProcessedAtUtc: null, updatedAtUtc: null }
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
let session: Employee | null
let status: AttendanceStatus
let mutation: () => Response | Promise<Response>
let lookup: (() => Response | Promise<Response>) | null
let history: (() => Response | Promise<Response>) | null
let details: AttendanceDetails
let correction: () => Response | Promise<Response>
let requests: { path: string; options: RequestInit }[]

beforeEach(() => {
  vi.restoreAllMocks()
  vi.spyOn(toast, 'success').mockImplementation(() => 'test-toast')
  session = { ...manager }; requests = []; lookup = null; history = null
  status = { employeeNumber: 42, firstName: 'דנה', lastName: 'כהן', hasOpenAttendance: false, checkInAtUtc: null }
  details = { record: { ...record }, corrections: [] }
  mutation = () => json({ employeeNumber: 42, employeeId: 'private-id' })
  correction = () => json({ record: { ...record, checkOutAtUtc: '2026-07-01T11:00:00Z', updatedAtUtc: '2026-07-01T12:00:00Z' }, corrections: [{ previousCheckInAtUtc: record.checkInAtUtc, previousCheckOutAtUtc: null, newCheckInAtUtc: record.checkInAtUtc, newCheckOutAtUtc: '2026-07-01T11:00:00Z', correctedAtUtc: '2026-07-01T12:00:00Z', correctedByEmployeeNumber: 1, correctedByFirstName: 'מנהלת', correctedByLastName: 'לוי', reason: 'יציאה חסרה' }] })
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit = {}) => {
    requests.push({ path, options })
    if (path === '/api/auth/me') return session ? json(session) : json({}, 401)
    if (path.includes('/corrections')) return correction()
    if (path.startsWith('/api/attendance/management/records?')) return history ? history() : json({ items: [details.record], total: 1, page: 1, pageSize: 25 })
    if (path.startsWith('/api/attendance/management/records/')) return json(details)
    if (path.endsWith('/status')) return lookup ? lookup() : json(status)
    if (path.startsWith('/api/attendance/')) return mutation()
    return json([])
  }))
})
function setup(path = '/attendance') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(<QueryClientProvider client={client}><AuthProvider><RouterProvider router={router} /></AuthProvider></QueryClientProvider>)
  return { router, client, user: userEvent.setup(), ...view }
}
const attendanceRequests = () => requests.filter(request => request.path.startsWith('/api/attendance/'))
async function ready() { await screen.findByRole('heading', { name: 'נוכחות' }) }
async function findEmployee(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('מספר עובד'), '42')
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  await screen.findByRole('heading', { name: 'דנה כהן' })
}

it.each([true, false])('allows authenticated manager=%s to look up before choosing attendance', async isManager => {
  session = { ...manager, isManager }
  const { user } = setup(); await ready()
  expect(screen.getByRole('link', { name: 'נוכחות' })).toHaveAttribute('aria-current', 'page')
  expect(screen.getByLabelText('מספר עובד').closest('[dir="rtl"]')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
  expect(attendanceRequests()).toHaveLength(0)
  await findEmployee(user)
  expect(screen.getByText('אין נוכחות פתוחה')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeEnabled()
  expect(screen.queryByRole('button', { name: 'יציאה' })).not.toBeInTheDocument()
  expect(attendanceRequests()[0].path).toBe('/api/attendance/42/status')
  expect(screen.queryByRole('link', { name: 'ניהול נוכחות' }) !== null).toBe(isManager)
})
it('redirects anonymous users to login without attendance requests', async () => {
  session = null
  const { router } = setup()
  await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
  expect(attendanceRequests()).toHaveLength(0)
})
it.each(['', '0', '-1', '1.5', '1e2', 'abc', '2147483648', '١٢'])('validates employee number %j before lookup', async value => {
  const { user } = setup(); await ready()
  if (value) await user.type(screen.getByLabelText('מספר עובד'), value)
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('מספר שלם חיובי')
  expect(screen.getByLabelText('מספר עובד')).toHaveFocus()
  expect(attendanceRequests()).toHaveLength(0)
})
it.each([false, true])('uses the contextual action for open=%s with cookies and a distinct subject', async open => {
  status.hasOpenAttendance = open; status.checkInAtUtc = open ? record.checkInAtUtc : null
  session = { ...manager, isManager: false }
  const { user } = setup(); await ready(); await findEmployee(user)
  await user.click(screen.getByRole('button', { name: open ? 'יציאה' : 'כניסה' }))
  expect(await screen.findByRole('status')).toHaveTextContent(`נרשמה בהצלחה עבור דנה כהן`)
  expect(attendanceRequests()[1]).toEqual({ path: `/api/attendance/42/${open ? 'check-out' : 'check-in'}`, options: { method: 'POST', credentials: 'same-origin', signal: expect.any(AbortSignal) } })
  expect(toast.success).toHaveBeenCalled()
  expect(screen.queryByText('private-id')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: open ? 'יציאה' : 'כניסה' })).not.toBeInTheDocument()
})
it('invalidates the selected employee when the number changes', async () => {
  const { user } = setup(); await ready(); await findEmployee(user)
  await user.type(screen.getByLabelText('מספר עובד'), '3')
  expect(screen.queryByRole('heading', { name: 'דנה כהן' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
})
it('locks lookup, input and action during a mutation and suppresses duplicate requests', async () => {
  let resolve!: (response: Response) => void
  mutation = () => new Promise(done => { resolve = done })
  const { user } = setup(); await ready(); await findEmployee(user)
  await user.dblClick(screen.getByRole('button', { name: 'כניסה' }))
  expect(screen.getByLabelText('מספר עובד')).toBeDisabled()
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'חיפוש עובד' })).toBeDisabled()
  expect(attendanceRequests()).toHaveLength(2)
  await act(async () => resolve(json({})))
  await waitFor(() => expect(screen.getByLabelText('מספר עובד')).toBeEnabled())
})
it('shows a safe lookup error without allowing mutation', async () => {
  lookup = () => json({ title: 'Employee was not found.', detail: 'Private detail' }, 404)
  const { user } = setup(); await ready()
  await user.type(screen.getByLabelText('מספר עובד'), '42')
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('העובד לא נמצא')
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
  expect(screen.queryByText('Private detail')).not.toBeInTheDocument()
})
it('conflict requires another lookup before retrying the action', async () => {
  mutation = () => json({ title: 'לעובד כבר קיימת כניסה פתוחה.' }, 409)
  const { user } = setup(); await ready(); await findEmployee(user)
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('לעובד כבר קיימת כניסה פתוחה.')
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
  expect(toast.success).not.toHaveBeenCalled()
})
it('allows a deliberate retry after network failure', async () => {
  mutation = () => { throw new Error('Private network failure') }
  const { user } = setup(); await ready(); await findEmployee(user)
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן להתחבר ל-OptiCore.')
  mutation = () => json({})
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('status')).toHaveTextContent('נרשמה בהצלחה')
})
it('honors a 401 response by returning to login', async () => {
  mutation = () => { session = null; return json({}, 401) }
  const { user, router } = setup(); await ready(); await findEmployee(user)
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
  expect(toast.success).not.toHaveBeenCalled()
})
it('aborts a late mutation response after navigation', async () => {
  let resolve!: (response: Response) => void
  mutation = () => new Promise(done => { resolve = done })
  const { user, router } = setup(); await ready(); await findEmployee(user)
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await act(async () => { await router.navigate('/customers') })
  expect(attendanceRequests()[1].options.signal?.aborted).toBe(true)
  await act(async () => resolve(json({})))
  expect(toast.success).not.toHaveBeenCalled()
})
it.each(['regular', 'anonymous'])('guards management for %s users', async kind => {
  session = kind === 'anonymous' ? null : { ...manager, isManager: false }
  const { router } = setup('/attendance/management')
  await waitFor(() => expect(router.state.location.pathname).toBe(kind === 'anonymous' ? '/login' : '/customers'))
  expect(attendanceRequests()).toHaveLength(0)
})
it('filters manager history by employee and inclusive date range, then opens a record without GUIDs', async () => {
  const { user } = setup('/attendance/management')
  await screen.findByRole('button', { name: 'פתיחת רשומה של דנה כהן' })
  await user.type(screen.getByLabelText('מספר עובד'), '42')
  await user.type(screen.getByLabelText('מתאריך'), '2026-07-01')
  await user.type(screen.getByLabelText('עד תאריך'), '2026-07-02')
  await user.click(screen.getByRole('button', { name: 'סינון' }))
  await waitFor(() => expect(attendanceRequests().some(request => request.path.includes('employeeNumber=42&from=2026-07-01&to=2026-07-02'))).toBe(true))
  await user.click(await screen.findByRole('button', { name: 'פתיחת רשומה של דנה כהן' }))
  const region = await screen.findByRole('region', { name: 'פרטי רשומה' })
  await within(region).findByRole('heading', { name: 'פרטי נוכחות — דנה כהן (42)' })
  expect(within(region).getByLabelText('זמן כניסה')).toHaveValue('2026-07-01T13:00')
  expect(within(region).getByText('אין תיקונים לרשומה זו.')).toBeInTheDocument()
  expect(screen.queryByText(record.id)).not.toBeInTheDocument()
})
it('requires a correction reason, adds missing checkout and displays returned audit', async () => {
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'פתיחת רשומה של דנה כהן' }))
  await screen.findByLabelText('סיבת התיקון')
  await user.click(screen.getByRole('button', { name: 'שמירת תיקון' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('סיבת התיקון נדרשת')
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
  await user.type(screen.getByLabelText('זמן יציאה'), '2026-07-01T14:00')
  await user.type(screen.getByLabelText('סיבת התיקון'), 'יציאה חסרה')
  await user.click(screen.getByRole('button', { name: 'שמירת תיקון' }))
  expect(await screen.findByRole('status')).toHaveTextContent('התיקון נשמר בהצלחה.')
  expect(await screen.findByText('סיבה: יציאה חסרה')).toBeInTheDocument()
  const request = attendanceRequests().find(r => r.options.method === 'POST')!
  expect(request.path).toBe(`/api/attendance/management/records/${record.id}/corrections`)
  expect(JSON.parse(request.options.body as string)).toEqual({ checkInAtUtc: record.checkInAtUtc, checkOutAtUtc: '2026-07-01T11:00:00.000Z', reason: 'יציאה חסרה', expectedCheckInAtUtc: record.checkInAtUtc, expectedCheckOutAtUtc: null, expectedUpdatedAtUtc: null })
  expect(screen.queryByText(record.id)).not.toBeInTheDocument()
})
it('shows backend stale correction conflicts and permits reloading', async () => {
  correction = () => json({ title: 'הרשומה השתנתה. יש לטעון מחדש לפני תיקון.' }, 409)
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'פתיחת רשומה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון')
  await user.click(screen.getByRole('button', { name: 'שמירת תיקון' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('הרשומה השתנתה')
  expect(screen.getByRole('button', { name: 'טעינה מחדש' })).toBeEnabled()
})
it('rejects checkout before check-in in the manager form', async () => {
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'פתיחת רשומה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון')
  await user.type(screen.getByLabelText('זמן יציאה'), '2026-07-01T12:00')
  await user.click(screen.getByRole('button', { name: 'שמירת תיקון' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('היציאה חייבת להיות לאחר הכניסה.')
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
})
it('shows empty history and validates invalid date ranges', async () => {
  history = () => json({ items: [], total: 0, page: 1, pageSize: 25 })
  const { user } = setup('/attendance/management')
  await screen.findByText('לא נמצאו רשומות נוכחות.')
  await user.type(screen.getByLabelText('מתאריך'), '2026-07-02')
  await user.type(screen.getByLabelText('עד תאריך'), '2026-07-01')
  await user.click(screen.getByRole('button', { name: 'סינון' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('טווח תאריכים תקין')
  expect(attendanceRequests()).toHaveLength(1)
})
it('shows history loading and a safe error with retry', async () => {
  let resolve!: (response: Response) => void
  history = () => new Promise(done => { resolve = done })
  setup('/attendance/management')
  expect(await screen.findByText('טוען היסטוריית נוכחות…')).toBeInTheDocument()
  await act(async () => resolve(json({ title: 'Private SQL data' }, 500)))
  expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן להשלים את הפעולה')
  expect(screen.getByRole('button', { name: 'ניסיון נוסף' })).toBeEnabled()
  expect(screen.queryByText('Private SQL data')).not.toBeInTheDocument()
})
it('revalidates manager access when backend denies history after demotion', async () => {
  history = () => { session = { ...manager, isManager: false }; return json({ title: 'Manager authorization is required.' }, 403) }
  const { router } = setup('/attendance/management')
  await waitFor(() => expect(router.state.location.pathname).toBe('/customers'))
  expect(screen.queryByRole('link', { name: 'ניהול נוכחות' })).not.toBeInTheDocument()
})
