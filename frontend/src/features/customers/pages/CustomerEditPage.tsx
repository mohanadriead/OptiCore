import { Link, useNavigate, useParams } from 'react-router'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { useCustomer, useRefreshCustomer } from '../api/customerQueries'
import { updateCustomer } from '../api/customerApi'
import { CustomerForm } from '../components/CustomerForm'
import { ErrorFeedback, Loading } from '../components/Feedback'
import { detailsPayload, type CustomerFormValues } from '../schemas/customerSchemas'
import { ApiError } from '@/lib/apiClient'
export function CustomerEditPage() {
  const number = Number(useParams().customerNumber)
  const result = useCustomer(number)
  const navigate = useNavigate()
  const refresh = useRefreshCustomer()
  const mutation = useMutation({ mutationFn: (values: CustomerFormValues) => updateCustomer(number, detailsPayload(values)),
    onSuccess: async customer => { await refresh(customer); toast.success('פרטי הלקוח עודכנו בהצלחה'); navigate('/customers/' + number) } })
  if (!Number.isInteger(number) || number <= 0) return <ErrorFeedback error={new ApiError(404, 'הלקוח לא נמצא.')} />
  if (result.isPending) return <Loading />
  if (result.isError) return <ErrorFeedback error={result.error} />
  const customer = result.data
  return <div className="max-w-4xl"><Link to={'/customers/' + number} className="text-sm text-primary">מספר לקוח <bdi dir="ltr">{number}</bdi></Link><h1 className="mb-2 mt-4">עריכת לקוח</h1>
    <p className="mb-6 text-sm text-muted-foreground">תעודת זהות: <bdi dir="ltr">{customer.nationalId}</bdi> · פרטי הזיהוי וההסכמה מנוהלים בנפרד.</p>
    {mutation.isError && <ErrorFeedback error={mutation.error} />}
    <CustomerForm key={number} editing initial={{ ...customer, homePhone: customer.homePhone ?? '', email: customer.email ?? '', street: customer.street ?? '', notes: customer.notes ?? '' }} pending={mutation.isPending} onSave={values => mutation.mutate(values)} cancelTo={'/customers/' + number} /></div>
}
