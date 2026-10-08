import { beforeEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { routes } from '@/app/router'
import { usePermissions } from '@/features/permissions/usePermissions'
import { permissionCodes, type EmployeePermissions, type Permission, type PermissionCode } from '@/features/permissions/permissionApi'
import type { Employee } from '@/features/employees/employeeApi'

const manager: Employee = { id: 'manager-test', employeeNumber: 1, firstName: 'Test', lastName: 'Manager', username: 'manager-test', phone: '0000000000', nationalId: '000000000', isActive: true, isManager: true, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: null }
const regular: Employee = { ...manager, id: 'regular-test', employeeNumber: 2, lastName: 'Employee', username: 'regular-test', isManager: false }
const labels = ['קבלת מלאי מספק', 'מתן הנחות', 'צפייה במכירות יומיות', 'צפייה ברווחים', 'צפייה בפרטי ספקים', 'צפייה בדוחות', 'ייצוא נתונים', 'צפייה ביומן ביקורת']
const catalog: Permission[] = permissionCodes.map((code, index) => ({ code, label: labels[index] }))
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const error = (status: number, title = 'private backend failure') => json({ title, detail: 'private server details' }, status)
let session: Employee | null
let assigned: PermissionCode[]
let requests: string[]
let handlers: Record<string, (options: RequestInit) => Response | Promise<Response>>
function target(number: number): EmployeePermissions {
  return { employeeNumber: number, isManager: number === 1, assignedPermissions: number === 1 ? ['ViewReports'] : assigned,
    effectivePermissions: number === 1 ? [...permissionCodes] : assigned }
}
beforeEach(() => {
  session = { ...manager }; assigned = ['ReceiveStock']; requests = []; handlers = {}
  localStorage.clear(); sessionStorage.clear()
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit = {}) => {
    const key = `${options.method || 'GET'} ${path}`
    requests.push(key)
    if (handlers[key]) return handlers[key](options)
    if (key === 'GET /api/auth/me') return session ? json(session) : error(401)
    if (key === 'GET /api/auth/permissions') return json(session?.isManager ? permissionCodes : assigned)
    if (key === 'GET /api/employees') return json([manager, regular])
    if (key === 'GET /api/permissions') return json(catalog)
    if (key === 'GET /api/employees/1/permissions') return json(target(1))
    if (key === 'GET /api/employees/2/permissions') return json(target(2))
    if (key === 'PUT /api/employees/2/permissions') { assigned = JSON.parse(options.body as string).permissions; return json(target(2)) }
    return error(500)
  }))
})
function PermissionProbe() {
  const permissions = usePermissions()
  return <section aria-label="permission probe">
    {permissionCodes.map(code => <output key={code} aria-label={code}>{String(permissions.hasPermission(code))}</output>)}
    <output aria-label="unknown permission">{String(permissions.hasPermission('AdjustInventory' as PermissionCode))}</output>
    <ButtonForProbe refresh={() => void permissions.refresh()} />
  </section>
}
function ButtonForProbe({ refresh }: { refresh: () => void }) { return <button onClick={refresh}>Refresh permissions</button> }
function setup({ path = '/employees', probe = false } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 30000 } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(<QueryClientProvider client={client}><AuthProvider><RouterProvider router={router} />{probe && <PermissionProbe />}</AuthProvider></QueryClientProvider>)
  return { user: userEvent.setup(), client, router }
}
async function openPermissions(user: ReturnType<typeof userEvent.setup>, name = 'Test Employee') {
  const table = await screen.findByRole('table')
  const row = within(table).getByText(name).closest('tr')!
  await user.click(within(row).getByRole('button', { name: 'הרשאות' }))
  return screen.getByRole('dialog')
}

it('offers manager permission actions, loads Hebrew catalog and assigned checkboxes', async () => {
  const { user } = setup()
  const dialog = await openPermissions(user)
  expect(await within(dialog).findByLabelText(labels[0])).toBeChecked()
  expect(within(dialog).getAllByRole('checkbox')).toHaveLength(8)
  for (const label of labels.slice(1)) expect(within(dialog).getByLabelText(label)).not.toBeChecked()
  expect(dialog).toHaveAttribute('dir', 'rtl')
  expect(requests).toContain('GET /api/permissions')
  expect(requests).toContain('GET /api/employees/2/permissions')
})
it('regular employees cannot access permission administration controls or APIs via the UI', async () => {
  session = { ...regular }
  const { router } = setup()
  await screen.findByRole('heading', { name: 'לקוחות' })
  expect(router.state.location.pathname).toBe('/customers')
  expect(screen.queryByRole('button', { name: 'הרשאות' })).not.toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'עובדים' })).not.toBeInTheDocument()
  expect(requests).toEqual(['GET /api/auth/me'])
})
it('saves the complete desired set and refreshes assignments and effective queries', async () => {
  const { user, client } = setup({ probe: true })
  await openPermissions(user)
  await user.click(await screen.findByLabelText(labels[0]))
  await user.click(screen.getByLabelText(labels[5]))
  await user.click(screen.getByLabelText(labels[6]))
  await user.click(screen.getByRole('button', { name: 'שמירת הרשאות' }))
  await waitFor(() => expect(assigned).toEqual(['ViewReports', 'ExportData']))
  await waitFor(() => expect(requests.filter(key => key === 'GET /api/employees/2/permissions')).toHaveLength(2))
  await waitFor(() => expect(requests.filter(key => key === 'GET /api/auth/permissions').length).toBeGreaterThan(1))
  expect(client.getQueryData(['employees', 2, 'permissions'])).toMatchObject({ assignedPermissions: ['ViewReports', 'ExportData'] })
  expect(screen.getByLabelText(labels[0])).not.toBeChecked()
  expect(screen.getByLabelText(labels[5])).toBeChecked()
  await user.click(screen.getByRole('button', { name: 'סיום' }))
  await openPermissions(user)
  expect(await screen.findByLabelText(labels[5])).toBeChecked()
  expect(screen.getByLabelText(labels[0])).not.toBeChecked()
  expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
})
it('allows replacing assignments with the empty set', async () => {
  const { user } = setup()
  await openPermissions(user)
  await user.click(await screen.findByLabelText(labels[0]))
  await user.click(screen.getByRole('button', { name: 'שמירת הרשאות' }))
  await waitFor(() => expect(assigned).toEqual([]))
})
it('disables editing, duplicate saves and dismissal while saving', async () => {
  let resolve!: (response: Response) => void
  handlers['PUT /api/employees/2/permissions'] = () => new Promise<Response>(done => { resolve = done })
  const { user } = setup()
  await openPermissions(user)
  await screen.findByLabelText(labels[0])
  await user.click(screen.getByRole('button', { name: 'שמירת הרשאות' }))
  expect(screen.getByRole('button', { name: 'שומר…' })).toBeDisabled()
  for (const checkbox of screen.getAllByRole('checkbox')) expect(checkbox).toBeDisabled()
  await user.keyboard('{Escape}')
  expect(screen.getByRole('dialog')).toBeVisible()
  expect(requests.filter(key => key === 'PUT /api/employees/2/permissions')).toHaveLength(1)
  await act(async () => resolve(json(target(2))))
  await waitFor(() => expect(screen.getByRole('button', { name: 'שמירת הרשאות' })).toBeEnabled())
})
it.each([
  [400, 'Unknown permission code.', 'אחת ההרשאות אינה מוכרת. רענן את הרשימה ונסה שוב.'],
  [400, 'Permissions are required.', 'יש לשלוח רשימת הרשאות.'],
  [500, 'private backend failure', 'לא ניתן להשלים את הפעולה. נסה שוב.'],
  [409, 'private unexpected conflict', 'לא ניתן להשלים את הפעולה. נסה שוב.'],
])('shows safe failure feedback without changing assignments %#', async (status, title, message) => {
  handlers['PUT /api/employees/2/permissions'] = () => error(status as number, title as string)
  const { user } = setup()
  await openPermissions(user)
  await user.click(await screen.findByLabelText(labels[5]))
  await user.click(screen.getByRole('button', { name: 'שמירת הרשאות' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message as string)
  expect(assigned).toEqual(['ReceiveStock'])
  expect(requests.filter(key => key === 'GET /api/employees/2/permissions')).toHaveLength(1)
  expect(screen.queryByText('private server details')).not.toBeInTheDocument()
})
it('shows Manager automatic-all permissions and offers no per-permission restriction', async () => {
  const { user } = setup()
  await openPermissions(user, 'Test Manager')
  expect(await screen.findByText('למנהלים יש את כל ההרשאות באופן אוטומטי')).toBeVisible()
  for (const checkbox of screen.getAllByRole('checkbox')) { expect(checkbox).toBeChecked(); expect(checkbox).toBeDisabled() }
  expect(screen.queryByRole('button', { name: 'שמירת הרשאות' })).not.toBeInTheDocument()
  await user.click(screen.getByLabelText(labels[0]))
  expect(screen.getByLabelText(labels[0])).toBeChecked()
  expect(requests.some(key => key.startsWith('PUT '))).toBe(false)
})
it('recovers from safe permission-load failure', async () => {
  handlers['GET /api/permissions'] = () => error(500)
  const { user } = setup()
  await openPermissions(user)
  expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן להשלים')
  delete handlers['GET /api/permissions']
  await user.click(screen.getByRole('button', { name: 'ניסיון נוסף' }))
  expect(await screen.findByLabelText(labels[0])).toBeChecked()
})
it('expires the session on permission API 401', async () => {
  handlers['GET /api/permissions'] = () => error(401)
  const { user } = setup()
  const table = await screen.findByRole('table')
  const row = within(table).getByText('Test Employee').closest('tr')!
  await user.click(within(row).getByRole('button', { name: 'הרשאות' }))
  await screen.findByRole('heading', { name: 'כניסה למערכת' })
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
})
it('refreshes manager truth and leaves administration after 403', async () => {
  handlers['PUT /api/employees/2/permissions'] = () => { session = { ...manager, isManager: false }; return error(403, 'Manager authorization is required.') }
  const { user } = setup()
  await openPermissions(user)
  await screen.findByLabelText(labels[0])
  await user.click(screen.getByRole('button', { name: 'שמירת הרשאות' }))
  await screen.findByRole('heading', { name: 'לקוחות' })
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
})
it('hasPermission grants regular Employees only their current effective assignments, without storage', async () => {
  session = { ...regular }
  const { user } = setup({ path: '/customers', probe: true })
  await waitFor(() => expect(screen.getByLabelText('ReceiveStock')).toHaveTextContent('true'))
  for (const code of permissionCodes.slice(1)) expect(screen.getByLabelText(code)).toHaveTextContent('false')
  expect(screen.getByLabelText('unknown permission')).toHaveTextContent('false')
  assigned = ['ViewReports']
  await user.click(screen.getByRole('button', { name: 'Refresh permissions' }))
  await waitFor(() => expect(screen.getByLabelText('ViewReports')).toHaveTextContent('true'))
  expect(screen.getByLabelText('ReceiveStock')).toHaveTextContent('false')
  expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0)
})
it('hasPermission resolves every catalog permission for Managers without assignment rows', async () => {
  assigned = []
  setup({ path: '/customers', probe: true })
  await screen.findByRole('heading', { name: 'לקוחות' })
  for (const code of permissionCodes) expect(screen.getByLabelText(code)).toHaveTextContent('true')
  expect(screen.getByLabelText('unknown permission')).toHaveTextContent('false')
})
it('hasPermission fails closed for regular Employees when fetching permissions fails', async () => {
  session = { ...regular }
  handlers['GET /api/auth/permissions'] = () => error(500)
  setup({ path: '/customers', probe: true })
  await screen.findByRole('heading', { name: 'לקוחות' })
  await waitFor(() => expect(requests).toContain('GET /api/auth/permissions'))
  for (const code of permissionCodes) expect(screen.getByLabelText(code)).toHaveTextContent('false')
})
