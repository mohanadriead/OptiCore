import { useState } from 'react'
import { Link } from 'react-router'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { errorMessage } from '@/lib/apiClient'
import { categories, genders, type Brand, type Category, type Gender, type Product, type ProductInput } from './catalogApi'
import { Field, Section, selectClass } from './CatalogFields'

export function ProductForm({ product, brands, onSave }: { product?: Product; brands: Brand[]; onSave: (input: ProductInput) => Promise<void> }) {
  const [category, setCategory] = useState<Category>(product?.category ?? 'Frames')
  const [barcode, setBarcode] = useState(product?.barcode ?? '')
  const [brandId, setBrandId] = useState(product?.brandId ?? '')
  const [model, setModel] = useState(product?.model ?? '')
  const [color, setColor] = useState(product?.color ?? '')
  const [size, setSize] = useState(product?.size ?? '')
  const [gender, setGender] = useState<Gender | ''>(product?.genderCategory ?? '')
  const [lens, setLens] = useState(product?.lensType ?? '')
  const [regular, setRegular] = useState(product ? String(product.regularSalePrice) : '')
  const [promo, setPromo] = useState(product?.promoPrice == null ? '' : String(product.promoPrice))
  const [attributes, setAttributes] = useState(product?.attributes ?? [])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const frame = category === 'Frames' || category === 'Sunglasses'
  const optional = (text: string) => text.trim() || null
  return <form className="space-y-5" onSubmit={async event => {
    event.preventDefault()
    if (busy) return
    setError('')
    if (promo !== '' && Number(promo) > Number(regular)) {
      setError('מחיר המבצע לא יכול להיות גבוה מהמחיר הרגיל.')
      return
    }
    const normalized = attributes.map(a => ({ key: a.key.trim(), value: a.value.trim() }))
    if (normalized.some(a => !a.key || !a.value)) { setError('יש למלא שם וערך לכל מאפיין.'); return }
    if (new Set(normalized.map(a => a.key)).size !== normalized.length) { setError('לא ניתן להזין שם מאפיין כפול.'); return }
    setBusy(true)
    try {
      await onSave({ category, barcode: optional(barcode), brandId: brandId || null, model: optional(model), color: optional(color), size: optional(size),
        genderCategory: frame ? gender || null : null, lensType: category === 'Lenses' ? optional(lens) : null,
        regularSalePrice: Number(regular), promoPrice: promo === '' ? null : Number(promo), attributes: normalized })
    } catch (failure) { setError(errorMessage(failure)) } finally { setBusy(false) }
  }}>
    <fieldset disabled={busy} className="space-y-5">
      <Section title="פרטי מוצר">
        <Field label="קטגוריה"><select className={selectClass} value={category} onChange={e => {
          const next = e.target.value as Category
          setCategory(next)
          if (next !== 'Frames' && next !== 'Sunglasses') setGender('')
          if (next !== 'Lenses') setLens('')
        }}>{Object.entries(categories).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></Field>
        <Field label="ברקוד"><Input type="text" value={barcode} maxLength={100} dir="ltr" onChange={e => setBarcode(e.target.value)} /></Field>
        <Field label="מותג"><select className={selectClass} value={brandId} onChange={e => setBrandId(e.target.value)}><option value="">ללא מותג</option>{brands.filter(b => b.isActive || b.id === product?.brandId).map(b => <option key={b.id} value={b.id}>{b.name}{b.isActive ? '' : ' (לא פעיל)'}</option>)}</select></Field>
        <Field label="דגם"><Input value={model} maxLength={200} onChange={e => setModel(e.target.value)} /></Field>
      </Section>
      <Section title="מאפיינים">
        <Field label="צבע"><Input value={color} maxLength={100} onChange={e => setColor(e.target.value)} /></Field>
        <Field label="מידה"><Input value={size} maxLength={100} onChange={e => setSize(e.target.value)} /></Field>
        {frame && <Field label="מגדר"><select className={selectClass} value={gender} onChange={e => setGender(e.target.value as Gender | '')}><option value="">ללא בחירה</option>{Object.entries(genders).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></Field>}
        {category === 'Lenses' && <Field label="סוג עדשה"><Input value={lens} maxLength={200} onChange={e => setLens(e.target.value)} /></Field>}
        <div className="space-y-3 sm:col-span-2"><h3 className="font-medium">מאפיינים נוספים</h3>{attributes.map((a, index) => <div className="flex flex-wrap items-end gap-3" key={index}>
          <Field label={`שם מאפיין ${index + 1}`}><Input required maxLength={100} value={a.key} onChange={e => setAttributes(attributes.map((item, i) => i === index ? { ...item, key: e.target.value } : item))} /></Field>
          <Field label={`ערך מאפיין ${index + 1}`}><Input required maxLength={500} value={a.value} onChange={e => setAttributes(attributes.map((item, i) => i === index ? { ...item, value: e.target.value } : item))} /></Field>
          <Button type="button" variant="outline" aria-label={`הסרת מאפיין ${index + 1}`} onClick={() => setAttributes(attributes.filter((_, i) => i !== index))}>הסרה</Button>
        </div>)}<Button type="button" variant="outline" onClick={() => setAttributes([...attributes, { key: '', value: '' }])}>הוספת מאפיין</Button></div>
      </Section>
      <Section title="מחירים">
        <Field label="מחיר מכירה רגיל"><Input type="number" dir="ltr" min="0" max="9999999999999999.99" step="0.01" required value={regular} onChange={e => setRegular(e.target.value)} /></Field>
        <Field label="מחיר מבצע"><Input type="number" dir="ltr" min="0" max="9999999999999999.99" step="0.01" value={promo} onChange={e => setPromo(e.target.value)} /></Field>
      </Section>
      {error && <p role="alert" className="text-destructive">{error}</p>}
      <div className="flex gap-3"><Button type="submit">{busy ? 'שומר…' : 'שמירת מוצר'}</Button>{!busy && <Button asChild variant="outline"><Link to={product ? `/products/${product.productNumber}` : '/products'}>ביטול</Link></Button>}</div>
    </fieldset>
  </form>
}
