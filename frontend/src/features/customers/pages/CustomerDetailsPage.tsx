import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Pencil } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter, DialogTrigger } from '@/components/ui/dialog'
import { useCustomer, useRefreshCustomer } from '../api/customerQueries'
import { setWhatsAppConsent, deactivateCustomer } from '../api/customerApi'
import { CustomerStatusBadge } from '../components/CustomerStatusBadge'
import { Loading, ErrorFeedback } from '../components/Feedback'
import { ApiError } from '@/lib/apiClient'
function Info({ label, value }: { label: string; value: string | null }) {
  return <div><dt className="mb-1 text-xs text-muted-foreground">{label}</dt><dd className="break-words whitespace-pre-wrap text-sm">{value || '—'}</dd></div>
}
export function CustomerDetailsPage() {
  const number = Number(useParams().customerNumber)
  const result = useCustomer(number)
  const refresh = useRefreshCustomer()
  const [confirm, setConfirm] = useState(false)
  const consent = useMutation({ mutationFn: (value: boolean) => setWhatsAppConsent(number, value),
    onSuccess: async customer => { await refresh(customer); toast.success('WhatsApp consent updated') } })
  const deactivate = useMutation({ mutationFn: () => deactivateCustomer(number), onSuccess: async () => {
    await refresh(); setConfirm(false); toast.success('Customer deactivated')
  } })
  if (!Number.isInteger(number) || number <= 0) return <ErrorFeedback error={new ApiError(404, 'Customer not found.')} />
  if (result.isPending) return <Loading />
  if (result.isError) return <><Link to="/customers">Back to customers</Link><ErrorFeedback error={result.error} /></>
  const customer = result.data
  const busy = consent.isPending || deactivate.isPending
  return <>
    <Link to="/customers" className="text-sm text-primary">Customers</Link>
    <div className="mb-7 mt-4 flex flex-wrap items-center justify-between gap-4"><div><div className="mb-2 flex items-center gap-3"><span className="text-sm text-muted-foreground">Customer #{customer.customerNumber}</span><CustomerStatusBadge active={customer.isActive} /></div><h1>{customer.firstName} {customer.lastName}</h1></div>
      <Button variant="outline" asChild><Link to={'/customers/' + number + '/edit'}><Pencil size={16} />Edit Customer</Link></Button></div>
    <div className="grid gap-5 lg:grid-cols-3">
      <Card className="lg:col-span-2"><CardHeader><CardTitle>Customer information</CardTitle></CardHeader><CardContent><dl className="grid gap-6 sm:grid-cols-2">
        <Info label="National ID" value={customer.nationalId} /><Info label="Date of birth" value={customer.dateOfBirth} />
        <Info label="Mobile phone" value={customer.mobilePhone} /><Info label="Home phone" value={customer.homePhone} />
        <Info label="Email" value={customer.email} /><Info label="Gender" value={customer.gender} />
        <Info label="City" value={customer.city} /><Info label="Street" value={customer.street} />
        <div className="sm:col-span-2 border-t pt-5"><Info label="Notes" value={customer.notes} /></div>
      </dl></CardContent></Card>
      <div className="space-y-5">
        <Card><CardHeader><CardTitle>Communication preferences</CardTitle></CardHeader><CardContent>
          <label className="flex items-center gap-3 text-sm font-medium"><input type="checkbox" className="size-4 accent-primary" checked={customer.whatsAppConsent} disabled={busy} onChange={event => consent.mutate(event.target.checked)} />WhatsApp consent</label>
          <p className="mt-2 text-xs text-muted-foreground">{consent.isPending ? 'Saving consent…' : customer.whatsAppConsent ? 'Consent given' : 'Consent not given'}</p>
          {consent.isError && <ErrorFeedback error={consent.error} />}
        </CardContent></Card>
        <Card><CardHeader><CardTitle>Record activity</CardTitle></CardHeader><CardContent><dl className="space-y-4">
          <Info label="Created" value={new Date(customer.createdAtUtc).toLocaleString()} /><Info label="Last updated" value={customer.updatedAtUtc ? new Date(customer.updatedAtUtc).toLocaleString() : 'Not updated yet'} />
        </dl></CardContent></Card>
      </div>
    </div>
    <div className="mt-6 flex flex-wrap items-center justify-between gap-4 rounded-lg border bg-white p-5"><div><h2 className="text-sm font-semibold">Customer status</h2><p className="mt-1 text-xs text-muted-foreground">{customer.isActive ? 'Deactivated customers remain available in search and history.' : 'This customer is inactive. Their record remains available.'}</p></div>
      <Dialog open={confirm} onOpenChange={open => { if (!deactivate.isPending) setConfirm(open) }}>
        <DialogTrigger asChild><Button variant="destructive" disabled={!customer.isActive || busy}>Deactivate Customer</Button></DialogTrigger>
        <DialogContent><DialogHeader><DialogTitle>Deactivate customer?</DialogTitle><DialogDescription>{customer.firstName} {customer.lastName} will become inactive. Their record will remain in the system and can still be viewed.</DialogDescription></DialogHeader>
          {deactivate.isError && <ErrorFeedback error={deactivate.error} />}
          <DialogFooter><Button variant="outline" disabled={deactivate.isPending} onClick={() => setConfirm(false)}>Cancel</Button><Button variant="destructive" disabled={deactivate.isPending} onClick={() => deactivate.mutate()}>{deactivate.isPending ? 'Deactivating…' : 'Confirm Deactivation'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  </>
}
