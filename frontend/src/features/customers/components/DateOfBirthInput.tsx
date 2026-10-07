import { useState, type Ref } from 'react'

type Props = {
  value: string; onChange: (value: string) => void; onBlur: () => void; ref?: Ref<HTMLSelectElement>
  id?: string; 'aria-describedby'?: string; 'aria-invalid'?: boolean | 'true' | 'false'
}

function dateParts(value: string) {
  const [year = '', month = '', day = ''] = value.split('-')
  return [day, month, year]
}

// The year range is a UI convenience, not a new validation rule. Keep stored outliers editable.
export function DateOfBirthInput({ value, onChange, onBlur, ref, id, ...aria }: Props) {
  const [selection, setSelection] = useState(() => ({ externalValue: value, emittedValue: null as string | null, parts: dateParts(value) }))
  if (value !== selection.externalValue) {
    // Preserve partial selections when the parent echoes our empty value, but
    // synchronize a different persisted date (or an external reset).
    setSelection({ externalValue: value, emittedValue: null, parts: value === selection.emittedValue ? selection.parts : dateParts(value) })
  }
  const parts = selection.parts
  const year = parts[2]
  const currentYear = new Date().getFullYear()
  const years = Array.from({ length: 121 }, (_, index) => String(currentYear - index))
  if (/^[0-9]{4}$/.test(year) && !years.includes(year)) years.push(year)
  years.sort((a, b) => Number(b) - Number(a))
  const options = [
    Array.from({ length: 31 }, (_, index) => String(index + 1).padStart(2, '0')),
    Array.from({ length: 12 }, (_, index) => String(index + 1).padStart(2, '0')),
    years,
  ]
  function change(index: number, next: string) {
    const updated = [...parts]
    updated[index] = next
    const emittedValue = updated.every(Boolean)
      ? `${updated[2].padStart(4, '0')}-${updated[1].padStart(2, '0')}-${updated[0].padStart(2, '0')}`
      : ''
    setSelection({ externalValue: value, emittedValue, parts: updated })
    onChange(emittedValue)
  }
  return <fieldset id={id} {...aria} className="min-w-0">
    <legend className="mb-2 text-sm font-medium">תאריך לידה *</legend>
    <div className="grid grid-cols-3 gap-3">
      {['יום', 'חודש', 'שנה'].map((label, index) => <div key={label} className="grid gap-1">
        <label htmlFor={`${id}-${index}`} className="text-xs text-muted-foreground">{label}</label>
        <select id={`${id}-${index}`} ref={index === 0 ? ref : undefined} dir="ltr"
          className="h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 text-sm shadow-xs focus-visible:outline-2 focus-visible:outline-ring disabled:opacity-50"
          value={parts[index] ? parts[index].padStart(index === 2 ? 4 : 2, '0') : ''} aria-required="true" {...aria}
          onBlur={onBlur} onChange={event => change(index, event.target.value)}>
          <option value="">{label}</option>
          {options[index].map(option => <option key={option} value={option}>{Number(option)}</option>)}
        </select>
      </div>)}
    </div>
  </fieldset>
}
