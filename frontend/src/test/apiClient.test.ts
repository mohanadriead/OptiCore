import { beforeEach, expect, it, vi } from 'vitest'
import { developmentActor } from '@/lib/developmentActor'
import { apiRequest } from '@/lib/apiClient'
import { searchCustomers, createCustomer } from '@/features/customers/api/customerApi'

beforeEach(() => { localStorage.clear() })
it('generates a stable nonempty UUID per development browser', () => {
  vi.stubEnv('DEV', true)
  const value = developmentActor()
  expect(value).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i)
  expect(developmentActor()).toBe(value)
})
it('does not invent an actor in a production build', () => {
  vi.stubEnv('DEV', false)
  expect(() => developmentActor()).toThrow('הזדהות')
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
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 409 })))
  await expect(apiRequest('/api/customers')).rejects.toMatchObject({ status: 409, message: 'לקוח עם תעודת זהות זו כבר קיים במערכת.' })
})
it('attaches actor only to customer mutations', async () => {
  vi.stubEnv('DEV', true)
  const fetch = vi.fn().mockResolvedValue(new Response('[]'))
  vi.stubGlobal('fetch', fetch)
  await searchCustomers('Ahmad')
  expect(fetch.mock.calls[0][1].headers).toBeUndefined()
  fetch.mockResolvedValue(new Response('{}'))
  await createCustomer({ nationalId: '123', firstName: 'A', lastName: 'B', dateOfBirth: '2000-01-01',
    mobilePhone: '123', city: 'City', gender: 'Text', homePhone: null, email: null, street: null, notes: null, whatsAppConsent: false })
  expect(fetch.mock.calls[1][1].headers['X-Employee-Id']).toBe(developmentActor())
})
