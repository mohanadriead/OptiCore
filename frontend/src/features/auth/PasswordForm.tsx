import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { errorMessage } from '@/lib/apiClient'
import { passwordChangeSchema } from '../employees/employeeSchemas'

type Values = z.infer<typeof passwordChangeSchema>
export function PasswordForm({ own = false, onSave, onBusyChange }: { own?: boolean; onSave: (current: string, next: string) => Promise<void>; onBusyChange?: (busy: boolean) => void }) {
  const schema = own ? passwordChangeSchema.refine(value => value.currentPassword.length > 0, { path: ['currentPassword'], message: 'יש להזין סיסמה נוכחית.' }) : passwordChangeSchema
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { currentPassword: '', newPassword: '', confirmation: '' } })
  const [error, setError] = useState('')
  const [success, setSuccess] = useState(false)
  const fields = [...(own ? [{ key: 'currentPassword' as const, label: 'סיסמה נוכחית', autocomplete: 'current-password' }] : []),
    { key: 'newPassword' as const, label: 'סיסמה חדשה', autocomplete: 'new-password' }, { key: 'confirmation' as const, label: 'אימות סיסמה חדשה', autocomplete: 'new-password' }]
  return <form noValidate className="space-y-4" onSubmit={handleSubmit(async values => {
    if (isSubmitting) return
    setError(''); setSuccess(false); onBusyChange?.(true)
    try { await onSave(values.currentPassword, values.newPassword); setSuccess(true) }
    catch (failure) { setError(errorMessage(failure)) }
    finally { reset(); onBusyChange?.(false) }
  })}>
    {fields.map(({ key, label, autocomplete }) => <div key={key}><label htmlFor={key}>{label}</label><Input id={key} type="password" dir="ltr" autoComplete={autocomplete} {...register(key)} disabled={isSubmitting} aria-invalid={!!errors[key]} aria-describedby={errors[key] ? `${key}-error` : undefined} />{errors[key] && <p id={`${key}-error`} className="text-sm text-destructive">{errors[key].message}</p>}</div>)}
    <p className="text-sm text-muted-foreground">הסיסמה החדשה חייבת להכיל בין 8 ל־128 תווים.</p>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {success && <p role="status">הסיסמה עודכנה בהצלחה.</p>}
    <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'שומר…' : 'שמירת סיסמה'}</Button>
  </form>
}
