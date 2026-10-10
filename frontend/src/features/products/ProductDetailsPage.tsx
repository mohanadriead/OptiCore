import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { errorMessage } from '@/lib/apiClient'
import { categories, genders, getProduct, price, setProductActive } from './catalogApi'

export function ProductDetailsPage() {
  const { productNumber } = useParams()
  const client = useQueryClient()
  const result = useQuery({ queryKey: ['products', productNumber], queryFn: ({ signal }) => getProduct(productNumber!, signal) })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  if (result.isPending) return <p role="status">טוען פרטי מוצר…</p>
  if (result.isError) return <div><p role="alert">{errorMessage(result.error)}</p><Button onClick={() => void result.refetch()}>ניסיון נוסף</Button></div>
  const p = result.data
  const fields = [['מספר מוצר', p.productNumber], ['ברקוד', p.barcode], ['קטגוריה', categories[p.category]], ['מותג', p.brandName ? p.brandName + (p.brandIsActive ? '' : ' (לא פעיל)') : null], ['דגם', p.model], ['צבע', p.color], ['מידה', p.size], ['מגדר', p.genderCategory ? genders[p.genderCategory] : null], ['סוג עדשה', p.lensType], ['מחיר מכירה רגיל', price(p.regularSalePrice)], ['מחיר מבצע', p.promoPrice == null ? null : price(p.promoPrice)], ['סטטוס', p.isActive ? 'פעיל' : 'לא פעיל']]
  return <div className="space-y-6"><div className="flex flex-wrap items-center justify-between gap-3"><h1>מוצר {p.productNumber}</h1><div className="flex gap-3"><Button asChild variant="outline"><Link to="/products">חזרה למוצרים</Link></Button><Button asChild><Link to={`/products/${p.productNumber}/edit`}>עריכת מוצר</Link></Button></div></div>
    <dl className="grid gap-5 rounded-lg border bg-white p-5 sm:grid-cols-3">{fields.map(([label, value]) => <div key={label}><dt className="text-sm text-muted-foreground">{label}</dt><dd className="mt-1 break-words font-medium"><bdi>{value ?? '—'}</bdi></dd></div>)}</dl>
    <section className="rounded-lg border bg-white p-5"><h2>מאפיינים נוספים</h2>{p.attributes.length ? <dl className="mt-4 grid gap-4 sm:grid-cols-3">{p.attributes.map(a => <div key={a.key}><dt className="break-words text-sm text-muted-foreground"><bdi>{a.key}</bdi></dt><dd className="break-words"><bdi>{a.value}</bdi></dd></div>)}</dl> : <p className="mt-3 text-muted-foreground">אין מאפיינים נוספים.</p>}</section>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    <Button variant="outline" disabled={busy} onClick={async () => {
      if (busy) return
      setBusy(true); setError('')
      try { await setProductActive(p); await client.invalidateQueries({ queryKey: ['products'] }) } catch (failure) { setError(errorMessage(failure)) } finally { setBusy(false) }
    }}>{busy ? 'שומר…' : p.isActive ? 'השבתת מוצר' : 'הפעלת מוצר מחדש'}</Button>
  </div>
}
