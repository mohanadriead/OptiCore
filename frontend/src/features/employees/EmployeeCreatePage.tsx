import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { ApiError, errorMessage } from '@/lib/apiClient'
import { useAuth } from '../auth/authContext'
import { createEmployee, type CreateEmployeeRequest } from './employeeApi'
import { employeeSchema } from './employeeSchemas'
import { digitInputHandlers } from '@/lib/digitInput'

const fields = [
  { key: 'nationalId', label: 'תעודת זהות' },
  { key: 'firstName', label: 'שם פרטי' }, { key: 'lastName', label: 'שם משפחה' },
  { key: 'phone', label: 'טלפון' },
  { key: 'username', label: 'שם משתמש' }, { key: 'password', label: 'סיסמה' },
] as const
export function EmployeeCreatePage() {
  const auth = useAuth()
  const client = useQueryClient()
  const navigate = useNavigate()
  const [error, setError] = useState('')
  const { register, handleSubmit, resetField, setValue, formState: { errors, isSubmitting } } = useForm<CreateEmployeeRequest>({ resolver: zodResolver(employeeSchema), defaultValues: { firstName: '', lastName: '', username: '', password: '', phone: '', nationalId: '', isManager: false } })
  return <div className="max-w-xl space-y-5"><Link to="/employees" className="text-primary">חזרה לעובדים</Link><h1>עובד חדש</h1>
    <form noValidate className="space-y-4 rounded-lg border bg-white p-5" onSubmit={handleSubmit(async values => {
      if (isSubmitting) return
      setError('')
      try {
        await createEmployee(values)
        await client.invalidateQueries({ queryKey: ['employees'] })
        toast.success('העובד נוצר בהצלחה')
        navigate('/employees')
      } catch (failure) {
        setError(errorMessage(failure))
        if (failure instanceof ApiError && failure.status === 403) await auth.refresh()
      } finally { resetField('password') }
    })}>
      <p className="text-sm text-muted-foreground">כל השדות המסומנים ב־* הם שדות חובה.</p>
      {fields.map(({ key, label }) => {
        const limit = key === 'nationalId' ? 9 : key === 'phone' ? 10 : undefined
        const field = register(key)
        return <div key={key}><label htmlFor={key}>{label}</label><span aria-hidden="true" className="text-destructive"> *</span>
          <Input id={key} {...field} required type={key === 'password' ? 'password' : 'text'} dir={key === 'firstName' || key === 'lastName' ? undefined : 'ltr'} inputMode={limit ? 'numeric' : undefined} maxLength={limit}
            {...(limit ? digitInputHandlers(limit, value => setValue(key, value, { shouldDirty: true, shouldValidate: true })) : {})}
            autoComplete={key === 'password' ? 'new-password' : 'off'} disabled={isSubmitting} aria-invalid={!!errors[key]} aria-describedby={errors[key] ? `${key}-error` : undefined} />
          {errors[key] && <p id={`${key}-error`} className="text-sm text-destructive">{errors[key].message}</p>}</div>
      })}
      <label className="flex items-center gap-2"><input type="checkbox" {...register('isManager')} disabled={isSubmitting} />מנהל</label>
      {error && <p role="alert" className="text-destructive">{error}</p>}
      <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'יוצר…' : 'יצירת עובד'}</Button>
    </form>
  </div>
}
