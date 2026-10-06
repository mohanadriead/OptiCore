import { useMutation } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { createCustomer } from '../api/customerApi'
import { useRefreshCustomer } from '../api/customerQueries'
import { CustomerForm } from '../components/CustomerForm'
import { ErrorFeedback } from '../components/Feedback'
import { detailsPayload, type CustomerFormValues } from '../schemas/customerSchemas'
export function CustomerCreatePage() {
  const navigate = useNavigate()
  const refresh = useRefreshCustomer()
  const mutation = useMutation({ mutationFn: (values: CustomerFormValues) => createCustomer({ ...detailsPayload(values), nationalId: values.nationalId, whatsAppConsent: values.whatsAppConsent }),
    onSuccess: async customer => { await refresh(customer); toast.success('הלקוח נוצר בהצלחה'); navigate('/customers/' + customer.customerNumber) } })
  return <div className="max-w-4xl"><Link to="/customers" className="text-sm text-primary">לקוחות</Link><h1 className="mb-2 mt-4">לקוח חדש</h1><p className="mb-6 text-sm text-muted-foreground">הזן פרטים אישיים ופרטי קשר ליצירת רשומת לקוח.</p>
    {mutation.isError && <ErrorFeedback error={mutation.error} />}<CustomerForm pending={mutation.isPending} onSave={values => mutation.mutate(values)} cancelTo="/customers" /></div>
}
