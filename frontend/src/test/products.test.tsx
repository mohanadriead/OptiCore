import { beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { routes } from '@/app/router'
import { categories, type Brand, type Product, type ProductInput } from '@/features/products/catalogApi'
import { apiRequest, ApiError } from '@/lib/apiClient'

const audit = { createdAtUtc: '2026-10-01T00:00:00Z', createdByEmployeeId: 'actor', updatedAtUtc: null, updatedByEmployeeId: null }
const original: Product = { ...audit, id: 'p1', productNumber: 1, barcode: '001AB', category: 'Frames', brandId: 'b1', brandName: 'Ray-Ban', brandIsActive: true,
  model: 'Classic', color: 'כחול', size: 'M', genderCategory: 'Unisex', lensType: null, regularSalePrice: 100, promoPrice: null, attributes: [], isActive: true }
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
let products: Product[]
let brands: Brand[]
let requests: { path: string; method: string; body?: Record<string, unknown> }[]
let conflict: 'product' | 'brand' | null
let signedIn: boolean

beforeEach(() => {
  products = [{ ...original }]
  brands = [{ ...audit, id: 'b1', name: 'Ray-Ban', isActive: true }, { ...audit, id: 'b2', name: 'Inactive', isActive: false }]
  requests = []; conflict = null; signedIn = true
  vi.stubGlobal('fetch', vi.fn(async (path: string, options: RequestInit = {}) => {
    const method = options.method ?? 'GET'
    const data = options.body ? JSON.parse(String(options.body)) : undefined
    requests.push({ path, method, body: data })
    const url = new URL(path, 'http://localhost')
    if (path === '/api/auth/me') return signedIn ? json({ ...audit, id: 'employee', employeeNumber: 1, firstName: 'Test', lastName: 'Employee', isManager: false, isActive: true }) : json({}, 401)
    if (path === '/api/auth/permissions') return json({ assignedPermissions: [], effectivePermissions: [] })
    if (url.pathname === '/api/brands' && method === 'GET') return json(brands)
    if (url.pathname.startsWith('/api/brands') && method !== 'GET') {
      if (conflict === 'brand') return json({ title: 'Brand name already exists.', detail: 'private server detail' }, 409)
      const id = url.pathname.split('/')[3]
      if (method === 'POST') { const brand = { ...audit, id: 'b3', name: data.name, isActive: true }; brands.push(brand); return json(brand, 201) }
      const brand = brands.find(b => b.id === id)!
      if (method === 'PUT') brand.name = data.name
      if (method === 'PATCH') brand.isActive = url.pathname.endsWith('/activate')
      return json(brand)
    }
    if (url.pathname === '/api/products' && method === 'GET') {
      let result = [...products]
      const status = url.searchParams.get('status')
      if (status !== 'all') result = result.filter(p => p.isActive === (status !== 'inactive'))
      const category = url.searchParams.get('category'), brandId = url.searchParams.get('brandId'), q = url.searchParams.get('q')
      if (category) result = result.filter(p => p.category === category)
      if (brandId) result = result.filter(p => p.brandId === brandId)
      if (q) result = result.filter(p => [String(p.productNumber), p.barcode, p.brandName, p.model, p.color].some(v => v?.toLowerCase().includes(q.toLowerCase())))
      return json({ items: result, total: result.length, page: Number(url.searchParams.get('page') ?? 1), pageSize: 50 })
    }
    if (url.pathname.startsWith('/api/products')) {
      if (method === 'POST' || method === 'PUT') {
        if (conflict === 'product') return json({ title: 'Product barcode already exists.', detail: 'private server detail' }, 409)
        const input = data as ProductInput
        const p = { ...original, ...input, productNumber: method === 'POST' ? 2 : 1, brandName: brands.find(b => b.id === input.brandId)?.name ?? null }
        if (method === 'POST') products.push(p); else products[0] = p
        return json(p, method === 'POST' ? 201 : 200)
      }
      const p = products.find(p => String(p.productNumber) === url.pathname.split('/')[3])
      if (!p) return json({ title: 'Product was not found.' }, 404)
      if (method === 'PATCH') p.isActive = url.pathname.endsWith('/activate')
      return json(p)
    }
    return json({}, 500)
  }))
})
function setup(path = '/products') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(<QueryClientProvider client={client}><AuthProvider><RouterProvider router={router} /></AuthProvider></QueryClientProvider>)
  return { user: userEvent.setup(), router }
}
async function fillPrices(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('מחיר מכירה רגיל'), '120.50')
}

it('shows Hebrew RTL product list and permits ordinary Employee access', async () => {
  setup()
  expect(await screen.findByRole('heading', { name: 'מוצרים' })).toBeVisible()
  const table = await screen.findByRole('table')
  expect(within(table).getByText('001AB')).toBeVisible()
  expect(within(table).getByText('מסגרות')).toBeVisible()
  expect(within(table).getByText('Ray-Ban / Classic')).toBeVisible()
  expect(screen.getByRole('main').closest('[dir]')).toHaveAttribute('dir', 'rtl')
})

it.each(['/products/new', '/products/1/edit'])('blocks a higher promo and removes VAT from %s', async path => {
  const { user } = setup(path)
  await screen.findByLabelText('מחיר מכירה רגיל')
  expect(screen.queryByLabelText(/מע[״"]מ|VAT/i)).not.toBeInTheDocument()
  await user.clear(screen.getByLabelText('מחיר מכירה רגיל'))
  await user.type(screen.getByLabelText('מחיר מכירה רגיל'), '100')
  await user.type(screen.getByLabelText('מחיר מבצע'), '100.01')
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('מחיר המבצע לא יכול להיות גבוה מהמחיר הרגיל.')
  expect(requests.some(r => r.method === 'POST' || r.method === 'PUT')).toBe(false)
  expect(screen.getByLabelText('מחיר מבצע')).toHaveValue(100.01)
})

it.each(['50', '100'])('accepts a lower or equal promo %s on edit without VAT in the request', async promo => {
  const { user } = setup('/products/1/edit')
  await screen.findByLabelText('מחיר מבצע')
  await user.type(screen.getByLabelText('מחיר מבצע'), promo)
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  await screen.findByRole('heading', { name: 'מוצר 1' })
  const request = requests.find(r => r.method === 'PUT')!
  expect(request.body).toMatchObject({ regularSalePrice: 100, promoPrice: Number(promo) })
  expect(request.body).not.toHaveProperty('vatRate')
  expect(screen.queryByText(/מע[״"]מ|VAT/i)).not.toBeInTheDocument()
})

it('translates the authoritative backend promo validation into Hebrew', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json({ title: 'Promo price cannot exceed regular price.' }, 400)))
  await expect(apiRequest('/api/products')).rejects.toEqual(new ApiError(400, 'מחיר המבצע לא יכול להיות גבוה מהמחיר הרגיל.'))
})

it('creates product with string barcode, brand dropdown, category and optional promo', async () => {
  const { user } = setup('/products/new')
  await screen.findByRole('heading', { name: 'מוצר חדש' })
  const barcode = screen.getByLabelText('ברקוד')
  expect(barcode).toHaveAttribute('type', 'text')
  await user.type(barcode, ' 001A-BC ')
  await user.selectOptions(screen.getByLabelText('מותג'), 'b1')
  expect(within(screen.getByLabelText('מותג')).queryByText('Inactive (לא פעיל)')).not.toBeInTheDocument()
  await user.selectOptions(screen.getByLabelText('קטגוריה'), 'Sunglasses')
  await fillPrices(user)
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  await screen.findByRole('heading', { name: 'מוצר 2' })
  const request = requests.find(r => r.method === 'POST' && r.path === '/api/products')!
  expect(request.body).toMatchObject({ barcode: '001A-BC', category: 'Sunglasses', brandId: 'b1', regularSalePrice: 120.5, promoPrice: null })
  expect(request.body).not.toHaveProperty('vatRate')
  expect(request.body).not.toHaveProperty('createdByEmployeeId')
})

it('supports all categories and clears stale conditional fields', async () => {
  const { user } = setup('/products/new')
  await screen.findByLabelText('קטגוריה')
  for (const label of Object.values(categories)) expect(within(screen.getByLabelText('קטגוריה')).getByText(label)).toBeVisible()
  await user.selectOptions(screen.getByLabelText('מגדר'), 'Kids')
  await user.selectOptions(screen.getByLabelText('קטגוריה'), 'Lenses')
  expect(screen.queryByLabelText('מגדר')).not.toBeInTheDocument()
  await user.type(screen.getByLabelText('סוג עדשה'), 'Type')
  await user.selectOptions(screen.getByLabelText('קטגוריה'), 'Other')
  expect(screen.queryByLabelText('סוג עדשה')).not.toBeInTheDocument()
  await user.selectOptions(screen.getByLabelText('קטגוריה'), 'Frames')
  expect(screen.getByLabelText('מגדר')).toHaveValue('')
  await fillPrices(user)
  await user.type(screen.getByLabelText('מחיר מבצע'), '120.50')
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  await screen.findByRole('heading', { name: 'מוצר 2' })
  expect(requests.find(r => r.method === 'POST')?.body).toMatchObject({ genderCategory: null, lensType: null, promoPrice: 120.5, barcode: null })
})

it('edits product and retains its inactive brand', async () => {
  brands[0].isActive = false; products[0].brandIsActive = false
  const { user } = setup('/products/1/edit')
  await screen.findByLabelText('מותג')
  expect(screen.getByLabelText('מותג')).toHaveValue('b1')
  expect(screen.getByRole('option', { name: 'Ray-Ban (לא פעיל)' })).toBeVisible()
  await user.clear(screen.getByLabelText('דגם')); await user.type(screen.getByLabelText('דגם'), 'Updated')
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  await screen.findByRole('heading', { name: 'מוצר 1' })
  expect(requests.find(r => r.method === 'PUT')?.body).toMatchObject({ brandId: 'b1', model: 'Updated', barcode: '001AB' })
})

it('adds edits and removes attributes, rejecting duplicate trimmed keys', async () => {
  products[0].attributes = [{ key: 'Finish', value: 'Old' }]
  const { user } = setup('/products/1/edit')
  await screen.findByLabelText('שם מאפיין 1')
  await user.clear(screen.getByLabelText('ערך מאפיין 1')); await user.type(screen.getByLabelText('ערך מאפיין 1'), 'New')
  await user.click(screen.getByRole('button', { name: 'הוספת מאפיין' }))
  await user.type(screen.getByLabelText('שם מאפיין 2'), ' Finish '); await user.type(screen.getByLabelText('ערך מאפיין 2'), 'Value')
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('שם מאפיין כפול')
  expect(requests.some(r => r.method === 'PUT')).toBe(false)
  await user.click(screen.getByRole('button', { name: 'הסרת מאפיין 2' }))
  await user.click(screen.getByRole('button', { name: 'שמירת מוצר' }))
  await screen.findByRole('heading', { name: 'מוצר 1' })
  expect(requests.find(r => r.method === 'PUT')?.body?.attributes).toEqual([{ key: 'Finish', value: 'New' }])
})

it('submits search category brand and active filters', async () => {
  const { user } = setup()
  await screen.findByRole('table')
  await user.type(screen.getByLabelText('חיפוש מוצרים'), '001AB')
  await user.click(screen.getByRole('button', { name: 'חיפוש' }))
  await user.selectOptions(screen.getByLabelText('סינון קטגוריה'), 'Frames')
  await user.selectOptions(screen.getByLabelText('סינון מותג'), 'b1')
  await user.selectOptions(screen.getByLabelText('סטטוס'), 'all')
  await waitFor(() => expect(requests.at(-1)?.path).toContain('brandId=b1'))
  const query = new URL(requests.at(-1)!.path, 'http://localhost').searchParams
  expect(Object.fromEntries(query)).toMatchObject({ q: '001AB', category: 'Frames', brandId: 'b1', status: 'all', page: '1' })
})

it('deactivates and reactivates a retrievable product', async () => {
  const { user } = setup('/products/1')
  await user.click(await screen.findByRole('button', { name: 'השבתת מוצר' }))
  await user.click(await screen.findByRole('button', { name: 'הפעלת מוצר מחדש' }))
  await screen.findByRole('button', { name: 'השבתת מוצר' })
  expect(requests.filter(r => r.method === 'PATCH').map(r => r.path)).toEqual(['/api/products/1/deactivate', '/api/products/1/activate'])
  expect(requests.some(r => r.method === 'DELETE')).toBe(false)
})

it('shows safe Hebrew duplicate barcode error without losing form input', async () => {
  conflict = 'product'
  const { user } = setup('/products/1/edit')
  await user.click(await screen.findByRole('button', { name: 'שמירת מוצר' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('מוצר עם ברקוד זה כבר קיים במערכת.')
  expect(screen.getByLabelText('ברקוד')).toHaveValue('001AB')
  expect(screen.queryByText('private server detail')).not.toBeInTheDocument()
})

it('manages brands including create rename deactivate and reactivate', async () => {
  const { user } = setup('/brands')
  await screen.findByRole('heading', { name: 'מותגים' })
  await user.type(screen.getByLabelText('שם מותג חדש'), ' New ')
  await user.click(screen.getByRole('button', { name: 'הוספת מותג' }))
  const row = (await screen.findByText('New')).closest('tr')!
  await user.click(within(row).getByRole('button', { name: 'עריכה' }))
  await user.clear(screen.getByLabelText('עריכת שם מותג')); await user.type(screen.getByLabelText('עריכת שם מותג'), 'Renamed')
  await user.click(screen.getByRole('button', { name: 'שמירת מותג' }))
  const renamed = (await screen.findByText('Renamed')).closest('tr')!
  await user.click(within(renamed).getByRole('button', { name: 'השבתה' }))
  await user.click(await within(renamed).findByRole('button', { name: 'הפעלה מחדש' }))
  await within(renamed).findByRole('button', { name: 'השבתה' })
  expect(requests.some(r => r.method === 'DELETE')).toBe(false)
})

it('shows safe Hebrew duplicate brand error', async () => {
  conflict = 'brand'
  const { user } = setup('/brands')
  await screen.findByLabelText('שם מותג חדש')
  await user.type(screen.getByLabelText('שם מותג חדש'), 'ray-ban')
  await user.click(screen.getByRole('button', { name: 'הוספת מותג' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('מותג בשם זה כבר קיים במערכת.')
  expect(screen.getByLabelText('שם מותג חדש')).toHaveValue('ray-ban')
})

it('exposes no cost inventory FIFO prescription or expiry input fields', async () => {
  setup('/products/new')
  await screen.findByLabelText('ברקוד')
  const form = screen.getByRole('button', { name: 'שמירת מוצר' }).closest('form')!
  expect(form.textContent).not.toMatch(/CostPrice|FIFO|SPH|CYL|עלות|כמות במלאי|תוקף|ספק/)
})

it.each(['/products', '/products/new', '/products/1', '/products/1/edit', '/brands'])('requires sign-in for %s', async path => {
  signedIn = false
  const { router } = setup(path)
  await screen.findByRole('heading', { name: 'כניסה למערכת' })
  expect(router.state.location.pathname).toBe('/login')
  expect(requests.some(r => r.path.startsWith('/api/products') || r.path.startsWith('/api/brands'))).toBe(false)
})
