import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { createSchema, emptyValues, type CustomerFormValues } from '../schemas/customerSchemas'
import { DateOfBirthInput } from './DateOfBirthInput'

const fields: { name: Exclude<keyof CustomerFormValues, 'whatsAppConsent'>; label: string; max?: number; required?: boolean; type?: string }[] = [
  { name: 'nationalId', label: 'תעודת זהות', max: 9, required: true },
  { name: 'firstName', label: 'שם פרטי', max: 100, required: true }, { name: 'lastName', label: 'שם משפחה', max: 100, required: true },
  { name: 'dateOfBirth', label: 'תאריך לידה', required: true },
  { name: 'mobilePhone', label: 'טלפון נייד', max: 10, required: true, type: 'tel' },
  { name: 'homePhone', label: 'טלפון בבית', max: 9, type: 'tel' }, { name: 'email', label: 'דוא״ל', max: 254 },
  { name: 'city', label: 'עיר', max: 100, required: true }, { name: 'street', label: 'רחוב', max: 200 },
  { name: 'gender', label: 'מגדר', required: true }, { name: 'notes', label: 'הערות', max: 4000 },
]
export function CustomerForm({ initial, editing = false, pending, onSave, cancelTo }: {
  initial?: CustomerFormValues; editing?: boolean; pending: boolean;
  onSave: (values: CustomerFormValues) => void; cancelTo: string
}) {
  const form = useForm<CustomerFormValues>({ resolver: zodResolver(createSchema), defaultValues: initial ?? emptyValues, mode: 'onChange' })
  return <Form {...form}><form noValidate onSubmit={form.handleSubmit(onSave)} className="rounded-lg border bg-white p-6">
    <p className="mb-6 text-sm text-muted-foreground">שדות המסומנים ב-* הם שדות חובה.</p>
    {editing && form.formState.errors.nationalId && <p role="alert" className="mb-4 text-sm text-destructive">
      תעודת הזהות ברשומה אינה תקינה. לא ניתן לתקן אותה במסך זה.
    </p>}
    <fieldset disabled={pending} className="grid gap-x-6 gap-y-5 sm:grid-cols-2">
      {fields.filter(field => !editing || field.name !== 'nationalId').map(({ name, label, max, required, type }) =>
        <FormField key={name} control={form.control} name={name} render={({ field }) =>
          <FormItem className={name === 'notes' ? 'sm:col-span-2' : ''}>
            {name === 'dateOfBirth' ? <FormControl><DateOfBirthInput {...field} /></FormControl> : name === 'gender' ?
              <FormControl><fieldset className="min-w-0"><legend className="mb-2 text-sm font-medium">מגדר *</legend>
                <div className="flex gap-6">{[['Male', 'זכר'], ['Female', 'נקבה']].map(([value, text], index) =>
                  <label key={value} className="flex items-center gap-2 text-sm"><input type="radio" name={field.name} value={value}
                    checked={field.value === value} onChange={() => field.onChange(value)} onBlur={field.onBlur}
                    ref={index === 0 ? field.ref : undefined} aria-required="true" className="size-4 accent-primary" />{text}</label>)}</div>
              </fieldset></FormControl> : <><FormLabel>{label}{required ? ' *' : ''}</FormLabel>
              <FormControl>{name === 'notes' ? <Textarea {...field} maxLength={max} rows={3} /> :
                <Input {...field} dir={['nationalId', 'mobilePhone', 'homePhone', 'email'].includes(name) ? 'ltr' : undefined}
                  inputMode={['nationalId', 'mobilePhone', 'homePhone'].includes(name) ? 'numeric' : undefined}
                  type={type ?? 'text'} maxLength={max} aria-required={required} />}</FormControl></>}
            <FormMessage />
          </FormItem>} />)}
      {!editing && <FormField control={form.control} name="whatsAppConsent" render={({ field }) =>
        <FormItem className="flex items-center gap-3 sm:col-span-2"><FormControl><input type="checkbox" checked={field.value} onChange={field.onChange} ref={field.ref} className="size-4 accent-primary" /></FormControl><FormLabel>הלקוח מסכים לקבל הודעות WhatsApp</FormLabel><FormMessage /></FormItem>} />}
    </fieldset>
    <div className="mt-7 flex justify-end gap-3 border-t pt-5"><Button variant="outline" asChild><Link to={cancelTo}>ביטול</Link></Button>
      <Button type="submit" disabled={pending}>{pending ? 'שומר…' : editing ? 'שמירת שינויים' : 'יצירת לקוח'}</Button></div>
  </form></Form>
}
