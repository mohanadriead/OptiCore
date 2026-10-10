import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { errorMessage } from '@/lib/apiClient'
import { categories, listBrands, listProducts, price, type Filters } from './catalogApi'
import { Field, selectClass } from './CatalogFields'

export function ProductsPage() {
  const [filters, setFilters] = useState<Filters>({ q: '', category: '', brandId: '', status: 'active', page: 1 })
  const [query, setQuery] = useState('')
  const brands = useQuery({ queryKey: ['brands'], queryFn: ({ signal }) => listBrands(signal) })
  const products = useQuery({ queryKey: ['products', filters], queryFn: ({ signal }) => listProducts(filters, signal) })
  const filter = (key: 'category' | 'brandId' | 'status', value: string) => setFilters({ ...filters, [key]: value, page: 1 })
  return <div className="space-y-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><h1>מוצרים</h1><div className="flex gap-3"><Button asChild variant="outline"><Link to="/brands">ניהול מותגים</Link></Button><Button asChild><Link to="/products/new">מוצר חדש</Link></Button></div></div>
    <form className="grid items-end gap-3 rounded-lg border bg-white p-5 sm:grid-cols-2 lg:grid-cols-5" onSubmit={e => { e.preventDefault(); setFilters({ ...filters, q: query.trim(), page: 1 }) }}>
      <Field label="חיפוש מוצרים"><Input maxLength={200} placeholder="מספר, ברקוד, מותג, דגם או צבע" value={query} onChange={e => setQuery(e.target.value)} /></Field>
      <Field label="סינון קטגוריה"><select className={selectClass} value={filters.category} onChange={e => filter('category', e.target.value)}><option value="">כל הקטגוריות</option>{Object.entries(categories).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></Field>
      <Field label="סינון מותג"><select className={selectClass} value={filters.brandId} onChange={e => filter('brandId', e.target.value)}><option value="">כל המותגים</option>{brands.data?.map(b => <option key={b.id} value={b.id}>{b.name}{b.isActive ? '' : ' (לא פעיל)'}</option>)}</select></Field>
      <Field label="סטטוס"><select className={selectClass} value={filters.status} onChange={e => filter('status', e.target.value)}><option value="active">פעילים</option><option value="inactive">לא פעילים</option><option value="all">הכול, כולל לא פעילים</option></select></Field>
      <Button type="submit">חיפוש</Button>
    </form>
    {brands.isError && <div><p role="alert">{errorMessage(brands.error)}</p><Button variant="outline" onClick={() => void brands.refetch()}>טעינת מותגים מחדש</Button></div>}
    {products.isPending && <p role="status">טוען מוצרים…</p>}
    {products.isError && <div><p role="alert">{errorMessage(products.error)}</p><Button onClick={() => void products.refetch()}>ניסיון נוסף</Button></div>}
    {products.data && <><div className="overflow-x-auto rounded-lg border bg-white"><table className="w-full text-sm"><caption className="sr-only">רשימת מוצרים</caption><thead><tr>{['מספר מוצר', 'ברקוד', 'קטגוריה', 'מותג / דגם', 'צבע / מידה', 'מחיר רגיל', 'מחיר מבצע', 'סטטוס'].map(label => <th className="whitespace-nowrap border-b p-3 text-start" key={label}>{label}</th>)}</tr></thead>
      <tbody>{products.data.items.map(p => <tr key={p.id} className="border-b last:border-0"><td className="p-3"><Link className="font-semibold text-primary underline" to={`/products/${p.productNumber}`}>{p.productNumber}</Link></td><td className="p-3"><bdi dir="ltr">{p.barcode ?? '—'}</bdi></td><td className="p-3">{categories[p.category]}</td><td className="p-3"><bdi>{[p.brandName, p.model].filter(Boolean).join(' / ') || '—'}</bdi></td><td className="p-3"><bdi>{[p.color, p.size].filter(Boolean).join(' / ') || '—'}</bdi></td><td className="whitespace-nowrap p-3">{price(p.regularSalePrice)}</td><td className="whitespace-nowrap p-3">{p.promoPrice == null ? '—' : price(p.promoPrice)}</td><td className="whitespace-nowrap p-3">{p.isActive ? 'פעיל' : 'לא פעיל'}</td></tr>)}</tbody></table>{!products.data.items.length && <p className="p-5">לא נמצאו מוצרים.</p>}</div>
      <div className="flex items-center gap-4"><p>{products.data.total} מוצרים · עמוד {products.data.page}</p><Button variant="outline" disabled={filters.page === 1} onClick={() => setFilters({ ...filters, page: filters.page - 1 })}>הקודם</Button><Button variant="outline" disabled={filters.page * products.data.pageSize >= products.data.total} onClick={() => setFilters({ ...filters, page: filters.page + 1 })}>הבא</Button></div></>}
  </div>
}
