import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { createSchema, emptyValues, type CustomerFormValues } from '../schemas/customerSchemas'

const fields: { name: Exclude<keyof CustomerFormValues, 'whatsAppConsent'>; label: string; max?: number; required?: boolean; type?: string }[] = [
  { name: 'nationalId', label: 'National ID', max: 9, required: true },
  { name: 'firstName', label: 'First Name', max: 100, required: true }, { name: 'lastName', label: 'Last Name', max: 100, required: true },
  { name: 'dateOfBirth', label: 'Date of Birth', required: true, type: 'date' },
  { name: 'mobilePhone', label: 'Mobile Phone', max: 30, required: true, type: 'tel' },
  { name: 'homePhone', label: 'Home Phone', max: 30, type: 'tel' }, { name: 'email', label: 'Email', max: 254 },
  { name: 'city', label: 'City', max: 100, required: true }, { name: 'street', label: 'Street', max: 200 },
  { name: 'gender', label: 'Gender', max: 50, required: true }, { name: 'notes', label: 'Notes', max: 4000 },
]
export function CustomerForm({ initial, editing = false, pending, onSave, cancelTo }: {
  initial?: CustomerFormValues; editing?: boolean; pending: boolean;
  onSave: (values: CustomerFormValues) => void; cancelTo: string
}) {
  const form = useForm<CustomerFormValues>({ resolver: zodResolver(createSchema), defaultValues: initial ?? emptyValues })
  return <Form {...form}><form noValidate onSubmit={form.handleSubmit(onSave)} className="rounded-lg border bg-white p-6">
    <p className="mb-6 text-sm text-muted-foreground">Fields marked * are required.</p>
    <fieldset disabled={pending} className="grid gap-x-6 gap-y-5 sm:grid-cols-2">
      {fields.filter(field => !editing || field.name !== 'nationalId').map(({ name, label, max, required, type }) =>
        <FormField key={name} control={form.control} name={name} render={({ field }) =>
          <FormItem className={name === 'notes' ? 'sm:col-span-2' : ''}>
            <FormLabel>{label}{required ? ' *' : ''}</FormLabel>
            <FormControl>{name === 'notes' ? <Textarea {...field} maxLength={max} rows={3} /> :
              <Input {...field} type={type ?? 'text'} maxLength={max} aria-required={required} />}</FormControl><FormMessage />
          </FormItem>} />)}
      {!editing && <FormField control={form.control} name="whatsAppConsent" render={({ field }) =>
        <FormItem className="flex items-center gap-3 sm:col-span-2"><FormControl><input type="checkbox" checked={field.value} onChange={field.onChange} ref={field.ref} className="size-4 accent-primary" /></FormControl><FormLabel>Customer consents to WhatsApp messages</FormLabel><FormMessage /></FormItem>} />}
    </fieldset>
    <div className="mt-7 flex justify-end gap-3 border-t pt-5"><Button variant="outline" asChild><Link to={cancelTo}>Cancel</Link></Button>
      <Button type="submit" disabled={pending}>{pending ? 'Saving…' : editing ? 'Save Changes' : 'Create Customer'}</Button></div>
  </form></Form>
}
