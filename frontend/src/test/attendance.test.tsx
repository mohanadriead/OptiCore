import { beforeEach, expect, it, vi } from 'vitest'
import { StrictMode } from 'react'
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
  mutation = () => json({ employeeNumber: 42, employeeId: 'private-id', checkInAtUtc: record.checkInAtUtc,
    checkOutAtUtc: status.hasOpenAttendance ? '2026-07-01T11:00:00Z' : null })
  correction = () => {
    details = { record: { ...record, checkOutAtUtc: '2026-07-01T11:00:00Z', updatedAtUtc: '2026-07-01T12:00:00Z' }, corrections: [{ previousCheckInAtUtc: record.checkInAtUtc, previousCheckOutAtUtc: null, newCheckInAtUtc: record.checkInAtUtc, newCheckOutAtUtc: '2026-07-01T11:00:00Z', correctedAtUtc: '2026-07-01T12:00:00Z', correctedByEmployeeNumber: 1, correctedByFirstName: 'מנהלת', correctedByLastName: 'לוי', reason: 'יציאה חסרה' }] }
    return json(details)
  }
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
function setup(path = '/attendance', strict = false) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const app = <QueryClientProvider client={client}><AuthProvider><RouterProvider router={router} /></AuthProvider></QueryClientProvider>
  const view = render(strict ? <StrictMode>{app}</StrictMode> : app)
  return { router, client, user: userEvent.setup(), ...view }
}
const attendanceRequests = () => requests.filter(request => request.path.startsWith('/api/attendance/'))
async function ready() { await screen.findByRole('heading', { name: 'נוכחות' }) }
async function findEmployee(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('מספר עובד'), '42')
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  await screen.findByRole('heading', { name: 'דנה כהן' })
}

it.each([true, false])('regression: retains the employee after HTTP 200 and supports check-in then check-out (manager=%s)', async isManager => {
  session = { ...manager, isManager }
  const { user } = setup(); await ready()
  if (isManager) await findEmployee(user)
  else {
    await screen.findByRole('heading', { name: 'דנה כהן' })
    expect(screen.queryByLabelText('מספר עובד')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'חיפוש עובד' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'ניהול נוכחות' })).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'מצב נוכחות' }).closest('[dir="rtl"]')).toBeInTheDocument()
  }
  mutation = () => json({ employeeNumber: 42, checkInAtUtc: record.checkInAtUtc, checkOutAtUtc: null })
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await screen.findByText(/הכניסה נרשמה בהצלחה/)
  expect(screen.getByRole('heading', { name: 'דנה כהן' })).toBeInTheDocument()
  expect(screen.getByRole('region', { name: 'מצב נוכחות' })).toHaveTextContent('נוכחות פתוחה')
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'יציאה' })).toBeEnabled()
  mutation = () => json({ employeeNumber: 42, checkInAtUtc: record.checkInAtUtc, checkOutAtUtc: '2026-07-01T11:00:00Z' })
  await user.click(screen.getByRole('button', { name: 'יציאה' }))
  await screen.findByText(/היציאה נרשמה בהצלחה/)
  expect(screen.getByRole('heading', { name: 'דנה כהן' })).toBeInTheDocument()
  expect(screen.getByRole('region', { name: 'מצב נוכחות' })).toHaveTextContent('אין נוכחות פתוחה')
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeEnabled()
  expect(screen.getByRole('button', { name: 'יציאה' })).toBeDisabled()
  expect(attendanceRequests().map(request => request.path)).toEqual([
    `/api/attendance/${isManager ? '42' : 'me'}/status`, `/api/attendance/${isManager ? '42' : 'me'}/check-in`, `/api/attendance/${isManager ? '42' : 'me'}/check-out`,
  ])
})

it.each([{ body: null }, { body: {} }, { body: [] }, { body: { employeeNumber: 42 } }])('regression: a malformed HTTP 200 lookup shows an error instead of missing employee details: $body', async ({ body }) => {
  lookup = () => json(body)
  const { user } = setup(); await ready()
  await user.type(screen.getByLabelText('מספר עובד'), '42')
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('פרטי הנוכחות שהתקבלו אינם תקינים')
  expect(screen.queryByRole('region', { name: 'מצב נוכחות' })).not.toBeInTheDocument()
  expect(toast.success).not.toHaveBeenCalled()
})

it('renders HTTP 200 lookup details with actual .NET UTC timestamp serialization and permits an explicit checkout', async () => {
  status.hasOpenAttendance = true
  status.checkInAtUtc = '2026-07-01T10:00:00.1234567+00:00'
  const { user } = setup(); await ready(); await findEmployee(user)
  expect(screen.getByRole('region', { name: 'מצב נוכחות' })).toHaveTextContent('דנה כהן')
  expect(screen.getByRole('region', { name: 'מצב נוכחות' })).toHaveTextContent('מספר עובד: 42')
  expect(screen.getByRole('region', { name: 'מצב נוכחות' })).toHaveTextContent('נוכחות פתוחה')
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'יציאה' })).toBeEnabled()
})

it('keeps attendance mutations explicit when Enter is used to look up the employee', async () => {
  const { user } = setup(); await ready()
  await user.type(screen.getByLabelText('מספר עובד'), '42{Enter}')
  await screen.findByRole('heading', { name: 'דנה כהן' })
  expect(attendanceRequests().filter(request => request.options.method === 'POST')).toHaveLength(0)
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeEnabled()
})

it('clears old details when a repeated lookup fails and permits a valid lookup retry', async () => {
  const { user } = setup(); await ready(); await findEmployee(user)
  lookup = () => json(null)
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('פרטי הנוכחות שהתקבלו אינם תקינים')
  expect(screen.queryByRole('heading', { name: 'דנה כהן' })).not.toBeInTheDocument()
  lookup = null
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  expect(await screen.findByRole('heading', { name: 'דנה כהן' })).toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})

it('does not offer an attendance action for a lookup response belonging to another employee', async () => {
  lookup = () => json({ ...status, employeeNumber: 43 })
  const { user } = setup(); await ready()
  await user.type(screen.getByLabelText('מספר עובד'), '42')
  await user.click(screen.getByRole('button', { name: 'חיפוש עובד' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('פרטי הנוכחות שהתקבלו אינם תקינים')
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
})

it('allows managers to look up before choosing attendance', async () => {
  const { user } = setup(); await ready()
  expect(screen.getByRole('link', { name: 'נוכחות' })).toHaveAttribute('aria-current', 'page')
  expect(screen.getByLabelText('מספר עובד').closest('[dir="rtl"]')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
  expect(attendanceRequests()).toHaveLength(0)
  await findEmployee(user)
  expect(screen.getByText('אין נוכחות פתוחה')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeEnabled()
  expect(screen.getByRole('button', { name: 'יציאה' })).toBeDisabled()
  expect(attendanceRequests()[0].path).toBe('/api/attendance/42/status')
  expect(screen.getByRole('link', { name: 'ניהול נוכחות' })).toBeInTheDocument()
})
it('redirects anonymous users to login without attendance requests', async () => {
  session = null
  const { router } = setup()
  await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
  expect(attendanceRequests()).toHaveLength(0)
})
it('automatically loads an open self session and offers checkout without search', async () => {
  session = { ...manager, isManager: false }
  status = { ...status, hasOpenAttendance: true, checkInAtUtc: record.checkInAtUtc }
  const { user } = setup()
  await screen.findByRole('heading', { name: 'דנה כהן' })
  expect(screen.getByText('מספר עובד: 42')).toBeInTheDocument()
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'כניסה' })).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'יציאה' }))
  await screen.findByText(/היציאה נרשמה בהצלחה/)
  expect(attendanceRequests().map(request => request.path)).toEqual(['/api/attendance/me/status', '/api/attendance/me/check-out'])
  expect(attendanceRequests()[1].options.body).toBeUndefined()
})
it('shows self-status loading and supports retry after a safe error', async () => {
  session = { ...manager, isManager: false }
  let resolve!: (response: Response) => void
  lookup = () => new Promise(done => { resolve = done })
  const { user } = setup()
  await screen.findByText('טוען את הנוכחות שלי…')
  expect(screen.getByRole('button', { name: 'רענון נוכחות' })).toBeDisabled()
  await act(async () => resolve(json({}, 500)))
  await screen.findByRole('alert')
  lookup = null
  await user.click(screen.getByRole('button', { name: 'רענון נוכחות' }))
  await screen.findByRole('heading', { name: 'דנה כהן' })
  expect(attendanceRequests().every(request => request.path === '/api/attendance/me/status')).toBe(true)
})
it('discards another employee after manager demotion and loads self status', async () => {
  const { user } = setup(); await ready(); await findEmployee(user)
  mutation = () => {
    session = { ...manager, isManager: false }
    status = { ...status, employeeNumber: 1, firstName: 'Test', lastName: 'Manager' }
    return json({ title: 'Manager authorization is required.' }, 403)
  }
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await screen.findByRole('heading', { name: 'Test Manager' })
  expect(screen.queryByRole('heading', { name: 'דנה כהן' })).not.toBeInTheDocument()
  expect(screen.queryByLabelText('מספר עובד')).not.toBeInTheDocument()
  expect(attendanceRequests().at(-1)?.path).toBe('/api/attendance/me/status')
})
it('loads self status under StrictMode and aborts superseded requests', async () => {
  session = { ...manager, isManager: false }
  setup('/attendance', true)
  await screen.findByRole('heading', { name: 'דנה כהן' })
  expect(attendanceRequests().at(-1)?.options.signal?.aborted).toBe(false)
  expect(attendanceRequests().every(request => request.path === '/api/attendance/me/status')).toBe(true)
  expect(screen.queryByRole('button', { name: 'חיפוש עובד' })).not.toBeInTheDocument()
})
it('requires self-status refresh after a conflict before retrying', async () => {
  session = { ...manager, isManager: false }
  mutation = () => json({ title: 'לעובד כבר קיימת כניסה פתוחה.' }, 409)
  const { user } = setup()
  await screen.findByRole('heading', { name: 'דנה כהן' })
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await screen.findByRole('alert')
  expect(screen.queryByRole('button', { name: 'כניסה' })).not.toBeInTheDocument()
  status = { ...status, hasOpenAttendance: true, checkInAtUtc: record.checkInAtUtc }
  await user.click(screen.getByRole('button', { name: 'רענון נוכחות' }))
  await screen.findByRole('heading', { name: 'דנה כהן' })
  expect(screen.getByRole('button', { name: 'יציאה' })).toBeEnabled()
  expect(attendanceRequests().map(request => request.path)).toEqual(['/api/attendance/me/status', '/api/attendance/me/check-in', '/api/attendance/me/status'])
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
  const { user } = setup(); await ready(); await findEmployee(user)
  await user.click(screen.getByRole('button', { name: open ? 'יציאה' : 'כניסה' }))
  expect(await screen.findByRole('status')).toHaveTextContent(`נרשמה בהצלחה עבור דנה כהן`)
  expect(attendanceRequests()[1]).toEqual({ path: `/api/attendance/42/${open ? 'check-out' : 'check-in'}`, options: { method: 'POST', credentials: 'same-origin', signal: expect.any(AbortSignal) } })
  expect(toast.success).toHaveBeenCalled()
  expect(screen.queryByText('private-id')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: open ? 'יציאה' : 'כניסה' })).toBeDisabled()
  expect(screen.getByRole('button', { name: open ? 'כניסה' : 'יציאה' })).toBeEnabled()
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
  await act(async () => resolve(json({ employeeNumber: 42, checkInAtUtc: record.checkInAtUtc, checkOutAtUtc: null })))
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
  mutation = () => json({ employeeNumber: 42, checkInAtUtc: record.checkInAtUtc, checkOutAtUtc: null })
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
  expect(within(region).queryByLabelText('שעת כניסה')).not.toBeInTheDocument()
  expect(within(region).getByText('אין תיקונים לרשומה זו.')).toBeInTheDocument()
  expect(screen.queryByText(record.id)).not.toBeInTheDocument()
})
it('requires a correction reason, adds missing checkout and displays returned audit', async () => {
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await screen.findByLabelText('סיבת התיקון')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('סיבת התיקון נדרשת')
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
  await user.type(screen.getByLabelText('תאריך יציאה'), '2026-07-01')
  await user.type(screen.getByLabelText('שעת יציאה'), '14:00')
  await user.type(screen.getByLabelText('סיבת התיקון'), 'יציאה חסרה')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  const confirmation = await screen.findByRole('dialog', { name: 'אישור שמירת שינויים' })
  expect(within(confirmation).getByText(/שעת יציאה:/)).toHaveTextContent('14:00')
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
  await user.click(within(confirmation).getByRole('button', { name: 'אישור ושמירה' }))
  expect(await screen.findByRole('status')).toHaveTextContent('התיקון נשמר בהצלחה.')
  expect(await screen.findByText('סיבה: יציאה חסרה')).toBeInTheDocument()
  await waitFor(() => expect(attendanceRequests().filter(r => r.path === `/api/attendance/management/records/${record.id}`)).toHaveLength(2))
  expect(attendanceRequests().filter(r => r.path.startsWith('/api/attendance/management/records?'))).toHaveLength(2)
  expect(screen.queryByLabelText('סיבת התיקון')).not.toBeInTheDocument()
  const request = attendanceRequests().find(r => r.options.method === 'POST')!
  expect(request.path).toBe(`/api/attendance/management/records/${record.id}/corrections`)
  expect(JSON.parse(request.options.body as string)).toEqual({ checkInAtUtc: record.checkInAtUtc, checkOutAtUtc: '2026-07-01T11:00:00.000Z', reason: 'יציאה חסרה', expectedCheckInAtUtc: record.checkInAtUtc, expectedCheckOutAtUtc: null, expectedUpdatedAtUtc: null })
  expect(screen.queryByText(record.id)).not.toBeInTheDocument()
})
it('shows backend stale correction conflicts and permits reloading', async () => {
  correction = () => json({ title: 'הרשומה השתנתה. יש לטעון מחדש לפני תיקון.' }, 409)
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  await user.click(await screen.findByRole('button', { name: 'אישור ושמירה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('הרשומה השתנתה')
  expect(screen.getByRole('button', { name: 'טעינה מחדש' })).toBeEnabled()
})
it('prepopulates Israel dates and HH:mm times and cancels without a correction', async () => {
  details.record.checkOutAtUtc = '2026-07-01T15:25:37Z'
  const { user } = setup('/attendance/management')
  const edit = await screen.findByRole('button', { name: 'עריכה של דנה כהן' })
  expect(edit).toHaveTextContent('עריכה')
  await user.click(edit)
  expect(await screen.findByLabelText('תאריך כניסה')).toHaveValue('2026-07-01')
  expect(screen.getByLabelText('שעת כניסה')).toHaveValue('13:00')
  expect(screen.getByLabelText('תאריך יציאה')).toHaveValue('2026-07-01')
  expect(screen.getByLabelText('שעת יציאה')).toHaveValue('18:25')
  await user.clear(screen.getByLabelText('שעת כניסה'))
  await user.type(screen.getByLabelText('שעת כניסה'), '12:45')
  await user.click(screen.getByRole('button', { name: 'ביטול' }))
  expect(screen.queryByLabelText('שעת כניסה')).not.toBeInTheDocument()
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
  await user.click(within(screen.getByRole('region', { name: 'פרטי רשומה' })).getByRole('button', { name: 'עריכה' }))
  expect(screen.getByLabelText('שעת כניסה')).toHaveValue('13:00')
})
it('lets the manager cancel confirmation without saving and keep the draft', async () => {
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון שעה')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  const dialog = await screen.findByRole('dialog')
  await user.click(within(dialog).getByRole('button', { name: 'ביטול' }))
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(screen.getByLabelText('סיבת התיקון')).toHaveValue('תיקון שעה')
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
})
it('edits both dates and times in Israel winter time and retains existing correction history', async () => {
  details.record = { ...record, checkInAtUtc: '2026-01-01T10:00:37.123Z', checkOutAtUtc: '2026-01-01T12:00:00Z',
    automaticCheckoutDueAtUtc: '2026-01-01T22:00:00Z', updatedAtUtc: '2026-01-01T13:00:00Z' }
  const previous = { previousCheckInAtUtc: '2026-01-01T10:00:00Z', previousCheckOutAtUtc: null,
    newCheckInAtUtc: details.record.checkInAtUtc, newCheckOutAtUtc: details.record.checkOutAtUtc,
    correctedAtUtc: details.record.updatedAtUtc!, correctedByEmployeeNumber: 1, correctedByFirstName: 'מנהלת', correctedByLastName: 'לוי', reason: 'תיקון קודם' }
  details.corrections = [previous]
  correction = () => {
    details = { record: { ...details.record, checkInAtUtc: '2026-01-02T06:15:00.000Z', checkOutAtUtc: '2026-01-02T14:45:00.000Z',
      updatedAtUtc: '2026-01-03T10:00:00Z' }, corrections: [previous, { ...previous,
      previousCheckInAtUtc: details.record.checkInAtUtc, previousCheckOutAtUtc: details.record.checkOutAtUtc,
      newCheckInAtUtc: '2026-01-02T06:15:00.000Z', newCheckOutAtUtc: '2026-01-02T14:45:00.000Z', reason: 'תיקון תאריך ושעה' }] }
    return json(details)
  }
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await screen.findByLabelText('שעת כניסה')
  for (const [label, value] of [['תאריך כניסה', '2026-01-02'], ['שעת כניסה', '08:15'], ['תאריך יציאה', '2026-01-02'], ['שעת יציאה', '16:45']]) {
    await user.clear(screen.getByLabelText(label)); await user.type(screen.getByLabelText(label), value)
  }
  await user.type(screen.getByLabelText('סיבת התיקון'), 'תיקון תאריך ושעה')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  const dialog = await screen.findByRole('dialog')
  expect(dialog).toHaveTextContent('08:15')
  expect(dialog).toHaveTextContent('16:45')
  await user.click(within(dialog).getByRole('button', { name: 'אישור ושמירה' }))
  await screen.findByText('התיקון נשמר בהצלחה.')
  expect(await screen.findByText('סיבה: תיקון קודם')).toBeInTheDocument()
  expect(await screen.findByText('סיבה: תיקון תאריך ושעה')).toBeInTheDocument()
  expect(JSON.parse(attendanceRequests().find(r => r.options.method === 'POST')!.options.body as string)).toEqual({
    checkInAtUtc: '2026-01-02T06:15:00.000Z', checkOutAtUtc: '2026-01-02T14:45:00.000Z', reason: 'תיקון תאריך ושעה',
    expectedCheckInAtUtc: '2026-01-01T10:00:37.123Z', expectedCheckOutAtUtc: '2026-01-01T12:00:00Z', expectedUpdatedAtUtc: '2026-01-01T13:00:00Z',
  })
})
it.each(['24:00', '12:99', '9:00', 'ab:cd'])('rejects an invalid 24-hour time %s before confirmation', async time => {
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון')
  await user.clear(screen.getByLabelText('שעת כניסה'))
  await user.type(screen.getByLabelText('שעת כניסה'), time)
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('HH:mm')
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(attendanceRequests().filter(r => r.options.method === 'POST')).toHaveLength(0)
})
it('revalidates authorization when correction saving is denied after manager demotion', async () => {
  correction = () => { session = { ...manager, isManager: false }; return json({}, 403) }
  const { user, router } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
  await user.click(await screen.findByRole('button', { name: 'אישור ושמירה' }))
  await waitFor(() => expect(router.state.location.pathname).toBe('/customers'))
  expect(screen.queryByLabelText('סיבת התיקון')).not.toBeInTheDocument()
})
it('rejects checkout before check-in in the manager form', async () => {
  const { user } = setup('/attendance/management')
  await user.click(await screen.findByRole('button', { name: 'עריכה של דנה כהן' }))
  await user.type(await screen.findByLabelText('סיבת התיקון'), 'תיקון')
  await user.type(screen.getByLabelText('תאריך יציאה'), '2026-07-01')
  await user.type(screen.getByLabelText('שעת יציאה'), '12:00')
  await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
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
