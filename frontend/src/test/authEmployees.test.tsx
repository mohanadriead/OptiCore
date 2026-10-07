import { beforeEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { routes } from '@/app/router'
import type { Employee } from '@/features/employees/employeeApi'
import { toast } from 'sonner'

// Entirely synthetic records and ephemeral form input; no real credentials or DB.
const manager: Employee = { id: 'manager-test', employeeNumber: 1, firstName: 'Test', lastName: 'Manager', username: 'manager-test', phone: '0000000000', nationalId: '000000000', isActive: true, isManager: true, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: null }
const employee: Employee = { ...manager, id: 'employee-test', employeeNumber: 2, firstName: 'Test', lastName: 'Employee', username: 'employee-test', isManager: false }
const ephemeralPassword = () => 'x'.repeat(12)
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const noContent = () => new Response(null, { status: 204 })
const problem = (status: number, title = 'Untrusted backend text') => json({ title, detail: 'Private backend detail' }, status)
type Handler = (options: RequestInit) => Response | Promise<Response>
let session: Employee | null
let employees: Employee[]
let handlers: Record<string, Handler>
let requests: string[]

beforeEach(() => {
  vi.restoreAllMocks()
  localStorage.clear(); sessionStorage.clear()
  session = { ...manager }
  employees = [{ ...manager }, { ...employee }, { ...employee, id: 'inactive-test', employeeNumber: 3, lastName: 'Inactive', isActive: false }]
  handlers = {}; requests = []
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit = {}) => {
    const key = `${options.method || 'GET'} ${path}`
    requests.push(key)
    if (handlers[key]) return handlers[key](options)
    if (key === 'GET /api/auth/me') return session ? json(session) : problem(401)
    if (key === 'POST /api/auth/login') { session = { ...employee }; return json(session) }
    if (key === 'POST /api/auth/logout') { session = null; return noContent() }
    if (key === 'GET /api/employees') return json(employees)
    if (key.startsWith('GET /api/customers/search')) return json([])
    return problem(500)
  }))
})
function setup(path = '/customers') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(<QueryClientProvider client={client}><AuthProvider><RouterProvider router={router} /></AuthProvider></QueryClientProvider>)
  return { client, router, user: userEvent.setup(), ...view }
}
const heading = (name: string) => screen.findByRole('heading', { name })
async function loginFields(user: ReturnType<typeof userEvent.setup>) {
  await heading('כניסה למערכת')
  await user.type(screen.getByLabelText('שם משתמש'), 'employee-test')
  await user.type(screen.getByLabelText('סיסמה'), ephemeralPassword())
}
async function createFields(user: ReturnType<typeof userEvent.setup>) {
  await heading('עובד חדש')
  for (const [label, value] of [['שם פרטי', 'Test'], ['שם משפחה', 'New'], ['שם משתמש', 'new-test'], ['טלפון', '0000000000'], ['תעודת זהות', '000000000'], ['סיסמה', ephemeralPassword()]]) await user.type(screen.getByLabelText(label), value)
}
function row(name = 'Test Employee') { return within(screen.getByRole('table')).getByText(name).closest('tr')! }
async function openAction(user: ReturnType<typeof userEvent.setup>, label: string, name?: string) {
  const table = await screen.findByRole('table')
  await within(table).findByText(name || 'Test Employee')
  await user.click(within(row(name)).getByRole('button', { name: label }))
  return screen.getByRole('dialog')
}

it.each(['/customers', '/customers/new', '/customers/42', '/customers/42/edit', '/employees', '/change-password'])('redirects unauthenticated %s to login', async path => {
  session = null
  const { router } = setup(path)
  await heading('כניסה למערכת')
  expect(router.state.location.pathname).toBe('/login')
  expect(requests).toEqual(['GET /api/auth/me'])
})
it('shows bootstrap loading without protected content or premature redirect', async () => {
  let resolve!: (response: Response) => void
  handlers['GET /api/auth/me'] = () => new Promise<Response>(done => { resolve = done })
  const { router } = setup('/employees')
  expect(screen.getByRole('status')).toHaveTextContent('בודק חיבור')
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  expect(router.state.location.pathname).toBe('/employees')
  await act(async () => resolve(json(manager)))
  await heading('עובדים')
})
it('bootstraps the current user and manager navigation in RTL', async () => {
  setup()
  await heading('לקוחות')
  expect(screen.getByText('Test Manager')).toBeVisible()
  expect(screen.getByRole('link', { name: 'עובדים' })).toBeVisible()
  expect(screen.getByRole('main').closest('[dir]')).toHaveAttribute('dir', 'rtl')
})
it('redirects an authenticated login visit into Customers', async () => {
  const { router } = setup('/login')
  await heading('לקוחות')
  expect(router.state.location.pathname).toBe('/customers')
})
it('keeps bootstrap network errors safe and supports retry', async () => {
  handlers['GET /api/auth/me'] = () => { throw new Error('Private network text') }
  const { user } = setup()
  expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן להתחבר')
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  delete handlers['GET /api/auth/me']
  await user.click(screen.getByRole('button', { name: 'ניסיון נוסף' }))
  await heading('לקוחות')
})
it('logs in with Enter, masks the password and persists no auth/password data', async () => {
  session = null
  const { user, client } = setup('/login')
  await loginFields(user)
  expect(screen.getByLabelText('סיסמה')).toHaveAttribute('type', 'password')
  await user.keyboard('{Enter}')
  await heading('לקוחות')
  expect(screen.getByText('Test Employee')).toBeVisible()
  expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
  expect(client.getMutationCache().getAll()).toHaveLength(0)
})
it('requires both login fields before submitting', async () => {
  session = null
  const { user } = setup('/login')
  await heading('כניסה למערכת')
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(requests).not.toContain('POST /api/auth/login')
})
it('disables login while pending to prevent duplicate submissions', async () => {
  session = null
  let resolve!: (response: Response) => void
  handlers['POST /api/auth/login'] = () => new Promise<Response>(done => { resolve = done })
  const { user } = setup('/login')
  await loginFields(user)
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(screen.getByRole('button', { name: 'מתחבר…' })).toBeDisabled()
  await user.keyboard('{Enter}')
  expect(requests.filter(key => key === 'POST /api/auth/login')).toHaveLength(1)
  await act(async () => resolve(json(employee)))
  await heading('לקוחות')
})
it('keeps login 401 as a safe form error without redirect loops', async () => {
  session = null
  handlers['POST /api/auth/login'] = () => problem(401, 'Invalid username or password.')
  const { user, router } = setup('/login')
  await loginFields(user)
  await user.click(screen.getByRole('button', { name: 'כניסה' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('שם המשתמש או הסיסמה שגויים.')
  expect(screen.getByLabelText('סיסמה')).toHaveValue('')
  expect(router.state.location.pathname).toBe('/login')
  expect(requests.filter(key => key === 'GET /api/auth/me')).toHaveLength(1)
})
it('clears protected caches on logout and guards browser back', async () => {
  const { user, client, router } = setup()
  await heading('לקוחות')
  client.setQueryData(['protected-test'], { value: 'private' })
  await user.click(screen.getByRole('button', { name: 'התנתקות' }))
  await heading('כניסה למערכת')
  expect(client.getQueryCache().getAll()).toHaveLength(0)
  expect(requests).toContain('POST /api/auth/logout')
  await act(async () => { await router.navigate('/customers') })
  await heading('כניסה למערכת')
})
it('does not claim logout succeeded when the request fails', async () => {
  handlers['POST /api/auth/logout'] = () => problem(500)
  const { user } = setup()
  await heading('לקוחות')
  await user.click(screen.getByRole('button', { name: 'התנתקות' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן להשלים')
  expect(screen.getByText('Test Manager')).toBeVisible()
})
it('ignores an in-flight create response arriving after logout', async () => {
  let resolve!: (response: Response) => void
  handlers['POST /api/employees'] = () => new Promise<Response>(done => { resolve = done })
  const { user, client, router } = setup('/employees/new')
  await createFields(user)
  await user.click(screen.getByRole('button', { name: 'יצירת עובד' }))
  await user.click(screen.getByRole('button', { name: 'התנתקות' }))
  await heading('כניסה למערכת')
  await act(async () => resolve(json(employee, 201)))
  expect(router.state.location.pathname).toBe('/login')
  expect(client.getQueryCache().getAll()).toHaveLength(0)
  expect(requests).not.toContain('GET /api/employees')
})
it('invalidates session and clears protected caches on a Customer 401', async () => {
  handlers['GET /api/customers/search?q=test'] = () => problem(401)
  const { user, client } = setup()
  await heading('לקוחות')
  await user.type(screen.getByLabelText('חיפוש לקוחות'), 'test')
  await user.click(screen.getByRole('button', { name: 'חיפוש' }))
  await heading('כניסה למערכת')
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  expect(client.getQueryCache().getAll()).toHaveLength(0)
})
it.each(['/customers', '/employees', '/employees/new'])('normal employees have no manager navigation and cannot stay on %s', async path => {
  session = { ...employee }
  const { router } = setup(path)
  await heading('לקוחות')
  expect(screen.queryByRole('link', { name: 'עובדים' })).not.toBeInTheDocument()
  expect(router.state.location.pathname).toBe('/customers')
  expect(requests).not.toContain('GET /api/employees')
})
it('lists active/inactive employees, identifiers and roles', async () => {
  setup('/employees')
  await screen.findByText('Test Inactive')
  expect(within(row('Test Inactive')).getByText('לא פעיל')).toBeVisible()
  expect(within(row('Test Inactive')).getByRole('button', { name: 'השבתה' })).toBeDisabled()
  expect(within(row('Test Manager')).getByText('מנהל')).toBeVisible()
  expect(within(row()).getByText('עובד', { exact: true })).toBeVisible()
  expect(screen.getByRole('columnheader', { name: 'תעודת זהות' })).toBeVisible()
})
it('validates Employee required fields and numeric/password constraints', async () => {
  const { user } = setup('/employees/new')
  await heading('עובד חדש')
  await user.click(screen.getByRole('button', { name: 'יצירת עובד' }))
  expect(await screen.findAllByText('שדה זה הוא שדה חובה.')).toHaveLength(3)
  expect(screen.getByText('תעודת זהות חייבת להכיל בדיוק 9 ספרות.')).toBeVisible()
  expect(screen.getByText('טלפון חייב להכיל בדיוק 10 ספרות.')).toBeVisible()
  expect(screen.getByText('הסיסמה חייבת להכיל בין 8 ל־128 תווים.')).toBeVisible()
  expect(screen.getByLabelText('תעודת זהות')).toHaveAttribute('type', 'text')
  expect(screen.getByLabelText('טלפון')).toHaveAttribute('inputmode', 'numeric')
  expect(requests).not.toContain('POST /api/employees')
})
it('creates an Employee, preserves leading zeros and refreshes the list without caching passwords', async () => {
  handlers['POST /api/employees'] = options => {
    const values = JSON.parse(options.body as string)
    expect(values.nationalId).toBe('000000000'); expect(values.phone).toBe('0000000000')
    expect(values.isManager).toBe(false)
    const created = { ...employee, id: 'new-test', employeeNumber: 4, firstName: values.firstName, lastName: values.lastName }
    employees.push(created)
    return json(created, 201)
  }
  const { user, client } = setup('/employees')
  await screen.findByText('Test Employee')
  await user.click(screen.getByRole('link', { name: 'עובד חדש' }))
  await createFields(user)
  await user.click(screen.getByRole('button', { name: 'יצירת עובד' }))
  expect(await screen.findByText('Test New')).toBeVisible()
  expect(requests.filter(key => key === 'GET /api/employees').length).toBeGreaterThan(1)
  expect(client.getMutationCache().getAll()).toHaveLength(0)
})
it.each([
  ['Username already exists.', 'שם המשתמש כבר קיים במערכת.'],
  ['An employee with this NationalId already exists.', 'עובד עם תעודת זהות זו כבר קיים במערכת.'],
  ['Untrusted backend text', 'לא ניתן להשלים את הפעולה. נסה שוב.'],
])('safely handles Employee create conflicts: %s', async (title, message) => {
  const success = vi.spyOn(toast, 'success')
  handlers['POST /api/employees'] = () => problem(409, title)
  const { user, router, client } = setup('/employees/new')
  await createFields(user)
  client.setQueryData(['employees'], [...employees])
  await user.click(screen.getByRole('button', { name: 'יצירת עובד' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.getByLabelText('סיסמה')).toHaveValue('')
  expect(screen.queryByText('Private backend detail')).not.toBeInTheDocument()
  expect(screen.queryByText('Untrusted backend text')).not.toBeInTheDocument()
  expect(router.state.location.pathname).toBe('/employees/new')
  expect(success).not.toHaveBeenCalled()
  expect(client.getQueryData(['employees'])).toEqual(employees)
  expect(requests.filter(key => key === 'POST /api/employees')).toHaveLength(1)
  expect(requests).not.toContain('GET /api/employees')
  expect(screen.getByLabelText('שם משתמש')).toHaveValue('new-test')
})
it('visibly marks all six create fields required, but not the manager checkbox', async () => {
  setup('/employees/new')
  await heading('עובד חדש')
  for (const label of ['שם פרטי', 'שם משפחה', 'שם משתמש', 'סיסמה', 'טלפון', 'תעודת זהות']) {
    const input = screen.getByLabelText(label)
    expect(input).toBeRequired()
    expect(within(input.parentElement!).getByText('*')).toBeVisible()
  }
  const checkbox = screen.getByRole('checkbox', { name: 'מנהל' })
  expect(checkbox).not.toBeRequired()
  expect(checkbox).not.toBeChecked()
  const form = checkbox.closest('form')!
  expect(Array.from(form.querySelectorAll('input')).map(input => input.name)).toEqual([
    'nationalId', 'firstName', 'lastName', 'phone', 'username', 'password', 'isManager',
  ])
})
it.each([
  ['שם פרטי', 'שדה זה הוא שדה חובה.'], ['שם משפחה', 'שדה זה הוא שדה חובה.'],
  ['שם משתמש', 'שדה זה הוא שדה חובה.'], ['סיסמה', 'הסיסמה חייבת להכיל בין 8 ל־128 תווים.'],
  ['טלפון', 'טלפון חייב להכיל בדיוק 10 ספרות.'], ['תעודת זהות', 'תעודת זהות חייבת להכיל בדיוק 9 ספרות.'],
])('rejects individually empty create field %s with linked Hebrew feedback', async (label, message) => {
  const { user } = setup('/employees/new')
  await createFields(user)
  const input = screen.getByLabelText(label)
  await user.clear(input)
  await user.click(screen.getByRole('button', { name: 'יצירת עובד' }))
  expect(await screen.findByText(message)).toBeVisible()
  expect(input).toHaveAttribute('aria-invalid', 'true')
  expect(input).toHaveAccessibleDescription(message)
  expect(requests).not.toContain('POST /api/employees')
})
it.each([['תעודת זהות', 9], ['טלפון', 10]] as const)('filters typing and caps %s without losing leading zeros', async (label, limit) => {
  const { user } = setup('/employees/new')
  await heading('עובד חדש')
  const input = screen.getByLabelText(label)
  expect(input).toHaveAttribute('type', 'text')
  expect(input).toHaveAttribute('inputmode', 'numeric')
  expect(input).toHaveAttribute('maxlength', String(limit))
  await user.type(input, 'abc٠١٢３４５')
  expect(input).toHaveValue('')
  await user.type(input, '00abc1234567890123')
  expect(input).toHaveValue('001234567890123'.slice(0, limit))
})
it.each([['תעודת זהות', 9], ['טלפון', 10]] as const)('filters pasted ASCII-only digits and caps %s', async (label, limit) => {
  const { user } = setup('/employees/new')
  await heading('עובד חדש')
  const input = screen.getByLabelText(label)
  await user.click(input)
  await user.paste('abc٠١٢３４５')
  expect(input).toHaveValue('')
  await user.paste('abc00٠١٢３４５1234567890123')
  expect(input).toHaveValue('001234567890123'.slice(0, limit))
})
it('requires explicit identified deactivation confirmation and refreshes the list', async () => {
  handlers['PATCH /api/employees/2/deactivate'] = () => { employees[1].isActive = false; return noContent() }
  const { user } = setup('/employees')
  const dialog = await openAction(user, 'השבתה')
  expect(within(dialog).getByText('Test Employee')).toBeVisible()
  expect(requests).not.toContain('PATCH /api/employees/2/deactivate')
  await user.click(within(dialog).getByRole('button', { name: 'ביטול' }))
  expect(requests).not.toContain('PATCH /api/employees/2/deactivate')
  await openAction(user, 'השבתה')
  await user.click(screen.getByRole('button', { name: 'אישור' }))
  await waitFor(() => expect(within(row()).getByText('לא פעיל')).toBeVisible())
})
it.each(['השבתה', 'הסרת ניהול'])('shows a safe last-manager conflict for %s', async label => {
  const endpoint = label === 'השבתה' ? 'deactivate' : 'manager-status'
  handlers[`PATCH /api/employees/1/${endpoint}`] = () => problem(409, 'At least one active manager must remain.')
  const { user } = setup('/employees')
  await openAction(user, label, 'Test Manager')
  await user.click(screen.getByRole('button', { name: 'אישור' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('חייב להישאר לפחות מנהל פעיל אחד.')
})
it('promotes then demotes another employee with confirmation and fresh list data', async () => {
  handlers['PATCH /api/employees/2/manager-status'] = options => {
    employees[1].isManager = JSON.parse(options.body as string).isManager
    return json(employees[1])
  }
  const { user } = setup('/employees')
  await openAction(user, 'מינוי למנהל')
  expect(requests).not.toContain('PATCH /api/employees/2/manager-status')
  await user.click(screen.getByRole('button', { name: 'אישור' }))
  await waitFor(() => expect(within(row()).getByText('מנהל', { exact: true })).toBeVisible())
  await openAction(user, 'הסרת ניהול')
  await user.click(screen.getByRole('button', { name: 'אישור' }))
  await waitFor(() => expect(within(row()).getByText('עובד', { exact: true })).toBeVisible())
})
it('refreshes /me immediately on self-demotion and removes manager UI/cache', async () => {
  handlers['PATCH /api/employees/1/manager-status'] = () => { session = { ...manager, isManager: false }; return json(session) }
  const { user, client } = setup('/employees')
  await openAction(user, 'הסרת ניהול', 'Test Manager')
  await user.click(screen.getByRole('button', { name: 'אישור' }))
  await heading('לקוחות')
  expect(requests.filter(key => key === 'GET /api/auth/me')).toHaveLength(2)
  expect(screen.queryByRole('link', { name: 'עובדים' })).not.toBeInTheDocument()
  expect(client.getQueryData(['employees'])).toBeUndefined()
})
it('refreshes the current role on a manager API 403', async () => {
  handlers['GET /api/employees'] = () => { session = { ...manager, isManager: false }; return problem(403, 'Manager authorization is required.') }
  setup('/employees')
  await heading('לקוחות')
  expect(screen.queryByRole('link', { name: 'עובדים' })).not.toBeInTheDocument()
})
it('refreshes session when returning to the window after a role change', async () => {
  setup()
  await heading('לקוחות')
  session = { ...manager, isManager: false }
  act(() => window.dispatchEvent(new Event('focus')))
  await waitFor(() => expect(screen.queryByRole('link', { name: 'עובדים' })).not.toBeInTheDocument())
})
it('validates manager reset length/confirmation, clears on close, and sends no current password', async () => {
  handlers['POST /api/employees/2/reset-password'] = options => {
    expect(Object.keys(JSON.parse(options.body as string))).toEqual(['newPassword'])
    return noContent()
  }
  const { user, client } = setup('/employees')
  await openAction(user, 'איפוס סיסמה')
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  expect(screen.getAllByText('הסיסמה חייבת להכיל בין 8 ל־128 תווים.')).toHaveLength(1)
  await user.type(screen.getByLabelText('סיסמה חדשה'), ephemeralPassword())
  await user.type(screen.getByLabelText('אימות סיסמה חדשה'), 'different')
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  expect(await screen.findByText('הסיסמאות אינן תואמות.')).toBeVisible()
  expect(requests).not.toContain('POST /api/employees/2/reset-password')
  await user.click(screen.getByRole('button', { name: 'סגירה' }))
  await openAction(user, 'איפוס סיסמה')
  expect(screen.getByLabelText('סיסמה חדשה')).toHaveValue('')
  await user.type(screen.getByLabelText('סיסמה חדשה'), ephemeralPassword())
  await user.type(screen.getByLabelText('אימות סיסמה חדשה'), ephemeralPassword())
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  expect(requests).toContain('POST /api/employees/2/reset-password')
  expect(client.getMutationCache().getAll()).toHaveLength(0)
})
it('validates own current/new/confirmation and clears all fields after success', async () => {
  session = { ...employee }
  handlers['POST /api/auth/change-password'] = () => noContent()
  const { user, client } = setup('/change-password')
  await heading('שינוי סיסמה')
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  expect(await screen.findByText('יש להזין סיסמה נוכחית.')).toBeVisible()
  expect(screen.getByText('הסיסמה חייבת להכיל בין 8 ל־128 תווים.')).toBeVisible()
  await user.type(screen.getByLabelText('סיסמה נוכחית'), ephemeralPassword())
  await user.type(screen.getByLabelText('סיסמה חדשה'), ephemeralPassword())
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  expect(await screen.findByText('הסיסמאות אינן תואמות.')).toBeVisible()
  await user.type(screen.getByLabelText('אימות סיסמה חדשה'), ephemeralPassword())
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  expect(await screen.findByRole('status')).toHaveTextContent('הסיסמה עודכנה בהצלחה.')
  for (const label of ['סיסמה נוכחית', 'סיסמה חדשה', 'אימות סיסמה חדשה']) expect(screen.getByLabelText(label)).toHaveValue('')
  expect(screen.getByText('Test Employee')).toBeVisible()
  expect(client.getMutationCache().getAll()).toHaveLength(0)
})
it.each([false, true])('distinguishes incorrect current password from an expired session (expired=%s)', async expired => {
  handlers['POST /api/auth/change-password'] = () => { if (expired) session = null; return problem(401, 'Invalid username or password.') }
  const { user } = setup('/change-password')
  await heading('שינוי סיסמה')
  for (const label of ['סיסמה נוכחית', 'סיסמה חדשה', 'אימות סיסמה חדשה']) await user.type(screen.getByLabelText(label), ephemeralPassword())
  await user.click(screen.getByRole('button', { name: 'שמירת סיסמה' }))
  if (expired) await heading('כניסה למערכת')
  else expect(await screen.findByRole('alert')).toHaveTextContent('הסיסמה הנוכחית שגויה.')
  expect(requests.filter(key => key === 'GET /api/auth/me')).toHaveLength(2)
})
