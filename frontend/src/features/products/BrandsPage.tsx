import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { errorMessage } from '@/lib/apiClient'
import { listBrands, saveBrand, setBrandActive, type Brand } from './catalogApi'
import { Field } from './CatalogFields'

export function BrandsPage() {
  const client = useQueryClient()
  const result = useQuery({ queryKey: ['brands'], queryFn: ({ signal }) => listBrands(signal) })
  const [editing, setEditing] = useState<Brand | null>(null)
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const refresh = async () => { await client.invalidateQueries({ queryKey: ['brands'] }); await client.invalidateQueries({ queryKey: ['products'] }) }
  return <div className="space-y-6"><div className="flex items-center justify-between"><h1>מותגים</h1><Button asChild variant="outline"><Link to="/products">חזרה למוצרים</Link></Button></div>
    <form className="rounded-lg border bg-white p-5" onSubmit={async e => {
      e.preventDefault()
      if (busy) return
      if (!name.trim()) { setError('יש להזין שם מותג.'); return }
      setBusy(true); setError('')
      try { await saveBrand(name, editing?.id); setEditing(null); setName(''); await refresh() } catch (failure) { setError(errorMessage(failure)) } finally { setBusy(false) }
    }}><fieldset disabled={busy} className="flex flex-wrap items-end gap-3"><Field label={editing ? 'עריכת שם מותג' : 'שם מותג חדש'}><Input required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></Field><Button type="submit">{busy ? 'שומר…' : editing ? 'שמירת מותג' : 'הוספת מותג'}</Button>{editing && <Button type="button" variant="outline" onClick={() => { setEditing(null); setName(''); setError('') }}>ביטול</Button>}</fieldset></form>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {result.isPending && <p role="status">טוען מותגים…</p>}
    {result.isError && <div><p role="alert">{errorMessage(result.error)}</p><Button onClick={() => void result.refetch()}>ניסיון נוסף</Button></div>}
    {result.data && <div className="overflow-x-auto rounded-lg border bg-white"><table className="w-full text-sm"><caption className="sr-only">רשימת מותגים</caption><thead><tr>{['שם מותג', 'סטטוס', 'פעולות'].map(label => <th key={label} className="border-b p-3 text-start">{label}</th>)}</tr></thead><tbody>{result.data.map(brand => <tr key={brand.id} className="border-b last:border-0"><td className="p-3"><bdi>{brand.name}</bdi></td><td className="p-3">{brand.isActive ? 'פעיל' : 'לא פעיל'}</td><td className="p-3"><div className="flex flex-wrap gap-3"><Button variant="outline" disabled={busy} onClick={() => { setEditing(brand); setName(brand.name); setError('') }}>עריכה</Button><Button variant="outline" disabled={busy} onClick={async () => {
      if (busy) return
      setBusy(true); setError('')
      try { await setBrandActive(brand); await refresh() } catch (failure) { setError(errorMessage(failure)) } finally { setBusy(false) }
    }}>{brand.isActive ? 'השבתה' : 'הפעלה מחדש'}</Button></div></td></tr>)}</tbody></table>{!result.data.length && <p className="p-5">לא נמצאו מותגים.</p>}</div>}
  </div>
}
