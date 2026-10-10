import { expect, it, vi } from 'vitest'
import { listBrands, listProducts } from '@/features/products/catalogApi'
import { searchCustomers } from '@/features/customers/api/customerApi'
import { apiRequest } from '@/lib/apiClient'

it('uses same-origin URLs and cookies for production API requests', async () => {
  vi.stubEnv('DEV', false)
  vi.stubEnv('PROD', true)
  const fetch = vi.fn().mockImplementation(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetch)
  await apiRequest('/api/auth/me')
  await searchCustomers('synthetic')
  await listProducts({ q: '', category: '', brandId: '', status: 'active', page: 1 })
  await listBrands()
  for (const [path, options] of fetch.mock.calls) {
    expect(path).toMatch(/^\/api\//)
    expect(path).not.toMatch(/localhost|https?:|onrender|neon/i)
    expect(options.credentials).toBe('same-origin')
  }
})
