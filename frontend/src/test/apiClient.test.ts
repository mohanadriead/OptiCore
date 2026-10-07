import { beforeEach, expect, it, vi } from 'vitest'
import { apiRequest } from '@/lib/apiClient'
import { discardSessionRequests, onSessionInvalidated } from '@/lib/sessionEvents'
import { searchCustomers, createCustomer } from '@/features/customers/api/customerApi'

beforeEach(() => { localStorage.clear() })
it.each([true, false])('uses browser cookies without inventing identity (DEV=%s)', async development => {
  vi.stubEnv('DEV', development)
  const fetch = vi.fn().mockResolvedValue(new Response('{}'))
  vi.stubGlobal('fetch', fetch)
  await apiRequest('/api/customers')
  expect(fetch).toHaveBeenCalledWith('/api/customers', { credentials: 'same-origin' })
  expect(localStorage.length).toBe(0)
})
it('handles 204 responses', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })))
  expect(await apiRequest('/api/customers/42/deactivate')).toBeUndefined()
})
it('hides unexpected ProblemDetails and database internals', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: 'Npgsql database secret', detail: 'stackTrace' }), { status: 500 })))
  await expect(apiRequest('/api/customers')).rejects.toMatchObject({ status: 500, message: 'לא ניתן להשלים את הפעולה. נסה שוב.' })
})
it('maps duplicate ProblemDetails safely', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: 'A customer with this NationalId already exists.' }), { status: 409 })))
  await expect(apiRequest('/api/customers')).rejects.toMatchObject({ status: 409, message: 'לקוח עם תעודת זהות זו כבר קיים במערכת.' })
})
it.each([
  [404, 'Customer was not found.', 'הלקוח לא נמצא. בדוק את מספר הלקוח או חזור לחיפוש.'],
  [404, 'Employee was not found.', 'העובד לא נמצא.'],
  [409, 'Username already exists.', 'שם המשתמש כבר קיים במערכת.'],
  [409, 'An employee with this NationalId already exists.', 'עובד עם תעודת זהות זו כבר קיים במערכת.'],
  [409, 'At least one active manager must remain.', 'חייב להישאר לפחות מנהל פעיל אחד.'],
  [401, 'Invalid username or password.', 'שם המשתמש או הסיסמה שגויים.'],
  [403, 'Employee is inactive.', 'העובד אינו פעיל.'],
  [403, 'Manager authorization is required.', 'נדרשת הרשאת מנהל.'],
])('translates only recognized status/title pair %#', async (status, title, message) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title, detail: 'Private internal detail' }), { status })))
  await expect(apiRequest('/api/resource')).rejects.toMatchObject({ status, message })
})
it.each([
  [400, { title: 'Unknown validation', detail: 'Private internal detail' }],
  [400, { title: 'constructor is required.' }],
  [404, { title: 'Unrelated resource was not found.' }],
  [404, {}],
  [404, { title: 'Customer was not found. Additional private text' }],
  [409, { title: 'Unrelated conflict', detail: 'Private internal detail' }],
  [409, {}],
  [409, { title: 'A customer with this NationalId already exists. Additional private text' }],
  [409, { detail: 'A customer with this NationalId already exists.' }],
  [500, { title: 'Customer was not found.' }],
  [500, { title: 'A customer with this NationalId already exists.' }],
  [500, { title: 'Unknown failure', detail: 'Private internal detail' }],
  [403, { title: { unexpected: 'Private internal detail' } }],
])('uses a generic safe message for unknown or mismatched errors %#', async (status, problem) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(problem), { status })))
  await expect(apiRequest('/api/resource')).rejects.toMatchObject({ status, message: 'לא ניתן להשלים את הפעולה. נסה שוב.' })
})
it('preserves network failure translation', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Private connection detail')))
  await expect(apiRequest('/api/resource')).rejects.toMatchObject({ status: 0, message: 'לא ניתן להתחבר ל-OptiCore. בדוק את החיבור ונסה שוב.' })
})
it('preserves AbortError identity', async () => {
  const abort = new Error('Cancelled')
  abort.name = 'AbortError'
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(abort))
  await expect(apiRequest('/api/resource')).rejects.toBe(abort)
})
it('ignores a stale 401 instead of invalidating the next session', async () => {
  let resolve!: (response: Response) => void
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(done => { resolve = done })))
  const invalidated = vi.fn()
  const unsubscribe = onSessionInvalidated(invalidated)
  try {
    const request = apiRequest('/api/resource')
    discardSessionRequests()
    resolve(new Response(null, { status: 401 }))
    await expect(request).rejects.toMatchObject({ name: 'AbortError' })
    expect(invalidated).not.toHaveBeenCalled()
  } finally { unsubscribe() }
})
it('never attaches a development actor to customer mutations', async () => {
  vi.stubEnv('DEV', true)
  const fetch = vi.fn().mockResolvedValue(new Response('[]'))
  vi.stubGlobal('fetch', fetch)
  await searchCustomers('Ahmad')
  expect(fetch.mock.calls[0][1].headers).toBeUndefined()
  fetch.mockResolvedValue(new Response('{}'))
  await createCustomer({ nationalId: '123', firstName: 'A', lastName: 'B', dateOfBirth: '2000-01-01',
    mobilePhone: '123', city: 'City', gender: 'Text', homePhone: null, email: null, street: null, notes: null, whatsAppConsent: false })
  expect(fetch.mock.calls[1][1].headers).toEqual({ 'Content-Type': 'application/json' })
  expect(localStorage.length).toBe(0)
})
