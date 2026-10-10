import { beforeEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { toast } from 'sonner'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { routes } from '@/app/router'
import type { Employee } from '@/features/employees/employeeApi'

const manager: Employee = { id: 'manager-test', employeeNumber: 1, firstName: 'Test', lastName: 'Manager', username: 'manager-test', phone: '0000000000', nationalId: '000000000', isActive: true, isManager: true, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: null }
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
let session: Employee | null
let mutation: () => Response | Promise<Response>
let requests: { path: string; options: RequestInit }[]

beforeEach(() => {
  vi.restoreAllMocks()
  vi.spyOn(toast, 'success').mockImplementation(() => 'test-toast')
  session = { ...manager }
  requests = []
  mutation = () => json({ employeeNumber: 42, employeeId: 'private-id', firstName: 'Private subject' })
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit = {}) => {
    requests.push({ path, options })
    if (path === '/api/auth/me') return session ? json(session) : json({}, 401)
    if (path.startsWith('/api/attendance/')) return mutation()
    return json([])
  }))
})
function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: ['/attendance'] })
  const view = render(<QueryClientProvider client={client}><AuthProvider><RouterProvider router={router} /></AuthProvider></QueryClientProvider>)
  return { router, user: userEvent.setup(), ...view }
}
const attendanceRequests = () => requests.filter(request => request.path.startsWith('/api/attendance/'))
async function ready() { await screen.findByRole('heading', { name: 'נוכחות' }) }

it('shows manager navigation and RTL without subject lookups or initial feedback', async () => {
  const { user } = setup()
  await ready()
  expect(screen.getByRole('link', { name: 'נוכחות' })).toHaveAttribute('aria-current', 'page')
  expect(screen.getByLabelText(/מספר עובד/).closest('[dir="rtl"]')).toBeInTheDocument()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.keyboard('{Enter}')
  expect(requests.map(request => request.path)).toEqual(['/api/auth/me'])
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
})
it.each(['regular', 'anonymous'])('guards direct access for %s users and hides navigation', async kind => {
  session = kind === 'anonymous' ? null : { ...manager, isManager: false }
  const { router } = setup()
  await screen.findByRole('heading', { name: kind === 'anonymous' ? 'כניסה למערכת' : 'לקוחות' })
  expect(router.state.location.pathname).toBe(kind === 'anonymous' ? '/login' : '/customers')
  expect(screen.queryByRole('link', { name: 'נוכחות' })).not.toBeInTheDocument()
  expect(attendanceRequests()).toHaveLength(0)
})
it.each(['', '0', '-1', '1.5', '1e2', 'abc', '2147483648', '١٢'])('validates invalid employee number %j only on submission', async value => {
  const { user } = setup()
  await ready()
  if (value) await user.type(screen.getByLabelText(/מספר עובד/), value)
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('מספר שלם חיובי')
  expect(screen.getByLabelText(/מספר עובד/)).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByLabelText(/מספר עובד/)).toHaveFocus()
  expect(attendanceRequests()).toHaveLength(0)
})
it.each([['כניסה', 'check-in', 'הכניסה'], ['יציאה', 'check-out', 'היציאה']])('submits %s with cookies and safe Hebrew success', async (label, action, prefix) => {
  const { user } = setup()
  await ready()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.click(screen.getByRole('button', { name: label }))
  expect(await screen.findByRole('status')).toHaveTextContent(`${prefix} נרשמה בהצלחה עבור עובד מספר 42.`)
  expect(attendanceRequests()).toEqual([{ path: `/api/attendance/42/${action}`, options: { method: 'POST', credentials: 'same-origin', signal: expect.any(AbortSignal) } }])
  expect(toast.success).toHaveBeenCalledWith(`${prefix} נרשמה בהצלחה עבור עובד מספר 42.`)
  expect(screen.queryByText('Private subject')).not.toBeInTheDocument()
  await user.type(screen.getByLabelText(/מספר עובד/), '3')
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
})
it('locks both actions and input during a pending request', async () => {
  let resolve!: (response: Response) => void
  mutation = () => new Promise(done => { resolve = done })
  const { user } = setup()
  await ready()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.dblClick(screen.getByRole('button', { name: 'כניסה' }))
  expect(screen.getByLabelText(/מספר עובד/)).toBeDisabled()
  for (const label of ['כניסה', 'יציאה']) expect(screen.getByRole('button', { name: label })).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'יציאה' }))
  expect(attendanceRequests()).toHaveLength(1)
  await act(async () => resolve(json({})))
  await waitFor(() => expect(screen.getByLabelText(/מספר עובד/)).toBeEnabled())
})
it.each([
  [409, 'לעובד כבר קיימת כניסה פתוחה.', 'לעובד כבר קיימת כניסה פתוחה.'],
  [409, 'לא קיימת כניסה פתוחה לעובד.', 'לא קיימת כניסה פתוחה לעובד.'],
  [403, 'לא ניתן לרשום כניסה לעובד לא פעיל.', 'לא ניתן לרשום כניסה לעובד לא פעיל.'],
  [404, 'Employee was not found.', 'העובד לא נמצא.'],
  [500, 'Private server data', 'לא ניתן להשלים את הפעולה. נסה שוב.'],
])('shows safe Hebrew failure for %s %s', async (status, title, message) => {
  mutation = () => json({ title, detail: 'Private detail' }, status)
  const { user } = setup()
  await ready()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(toast.success).not.toHaveBeenCalled()
  expect(screen.queryByText('Private detail')).not.toBeInTheDocument()
})
it.each([401, 403])('respects backend authorization rejection %s', async status => {
  mutation = () => {
    session = status === 401 ? null : { ...manager, isManager: false }
    return json({ title: 'Manager authorization is required.' }, status)
  }
  const { user, router } = setup()
  await ready()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await screen.findByRole('heading', { name: status === 401 ? 'כניסה למערכת' : 'לקוחות' })
  expect(router.state.location.pathname).toBe(status === 401 ? '/login' : '/customers')
  expect(toast.success).not.toHaveBeenCalled()
})
it('suppresses feedback from a request completing after navigation', async () => {
  let resolve!: (response: Response) => void
  mutation = () => new Promise(done => { resolve = done })
  const { user, router } = setup()
  await ready()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  await act(async () => { await router.navigate('/customers') })
  expect(attendanceRequests()[0].options.signal?.aborted).toBe(true)
  await act(async () => resolve(json({})))
  expect(toast.success).not.toHaveBeenCalled()
})
it('shows a Hebrew network error and allows a deliberate retry', async () => {
  mutation = () => { throw new Error('Private network failure') }
  const { user } = setup()
  await ready()
  await user.type(screen.getByLabelText(/מספר עובד/), '42')
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן להתחבר ל-OptiCore.')
  mutation = () => json({})
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('status')).toHaveTextContent('הכניסה נרשמה בהצלחה')
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  expect(attendanceRequests()).toHaveLength(2)
})
