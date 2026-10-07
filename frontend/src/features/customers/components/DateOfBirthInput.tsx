import type { Ref } from 'react'
import { Input } from '@/components/ui/input'

type Props = {
  value: string; onChange: (value: string) => void; onBlur: () => void; ref?: Ref<HTMLInputElement>
  id?: string; 'aria-describedby'?: string; 'aria-invalid'?: boolean | 'true' | 'false'
}

// Keep partial entry controlled without padding while typing; the schema emits canonical YYYY-MM-DD.
export function DateOfBirthInput({ value, onChange, onBlur, ref, id, ...aria }: Props) {
  const [year = '', month = '', day = ''] = value.split('-')
  const parts = [day, month, year]
  function change(index: number, next: string) {
    const updated = [...parts]
    updated[index] = next
    onChange(updated.every(part => part === '') ? '' : `${updated[2]}-${updated[1]}-${updated[0]}`)
  }
  return <fieldset id={id} {...aria} className="min-w-0">
    <legend className="mb-2 text-sm font-medium">תאריך לידה *</legend>
    <div className="grid grid-cols-3 gap-3">
      {['יום', 'חודש', 'שנה'].map((label, index) => <div key={label} className="grid gap-1">
        <label htmlFor={`${id}-${index}`} className="text-xs text-muted-foreground">{label}</label>
        <Input id={`${id}-${index}`} ref={index === 0 ? ref : undefined} type="text" inputMode="numeric" dir="ltr"
          placeholder={label} maxLength={index === 2 ? 4 : 2} value={parts[index]} aria-required="true" {...aria}
          onBlur={onBlur} onChange={event => change(index, event.target.value)} />
      </div>)}
    </div>
  </fieldset>
}
