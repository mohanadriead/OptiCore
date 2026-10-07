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
import { formatDateOnly, formatTimestamp } from '@/lib/dateFormat'
function Info({ label, value, ltr = false }: { label: string; value: string | null; ltr?: boolean }) {
  return <div><dt className="mb-1 text-xs text-muted-foreground">{label}</dt><dd className="break-words whitespace-pre-wrap text-sm"><bdi dir={ltr ? 'ltr' : 'auto'}>{value || '—'}</bdi></dd></div>
}
export function CustomerDetailsPage() {
  const number = Number(useParams().customerNumber)
  const result = useCustomer(number)
  const refresh = useRefreshCustomer()
  const [confirm, setConfirm] = useState(false)
  const consent = useMutation({ mutationFn: (value: boolean) => setWhatsAppConsent(number, value),
    onSuccess: async customer => { await refresh(customer); toast.success('הסכמת WhatsApp עודכנה') } })
  const deactivate = useMutation({ mutationFn: () => deactivateCustomer(number), onSuccess: async () => {
    await refresh(); setConfirm(false); toast.success('הלקוח הושבת בהצלחה')
  } })
  if (!Number.isInteger(number) || number <= 0) return <ErrorFeedback error={new ApiError(404, 'הלקוח לא נמצא.')} />
  if (result.isPending) return <Loading />
  if (result.isError) return <><Link to="/customers">חזרה ללקוחות</Link><ErrorFeedback error={result.error} /></>
  const customer = result.data
  const busy = consent.isPending || deactivate.isPending
  return <>
    <Link to="/customers" className="text-sm text-primary">לקוחות</Link>
    <div className="mb-7 mt-4 flex flex-wrap items-center justify-between gap-4"><div><div className="mb-2 flex items-center gap-3"><span className="text-sm text-muted-foreground">מספר לקוח <bdi dir="ltr">{customer.customerNumber}</bdi></span><CustomerStatusBadge active={customer.isActive} /></div><h1>{customer.firstName} {customer.lastName}</h1></div>
      <Button variant="outline" asChild><Link to={'/customers/' + number + '/edit'}><Pencil size={16} />עריכת לקוח</Link></Button></div>
    <div className="grid gap-5 lg:grid-cols-3">
      <Card className="lg:col-span-2"><CardHeader><CardTitle>פרטי לקוח</CardTitle></CardHeader><CardContent><dl className="grid gap-6 sm:grid-cols-2">
        <Info label="תעודת זהות" ltr value={customer.nationalId} /><Info label="תאריך לידה" ltr value={formatDateOnly(customer.dateOfBirth)} />
        <Info label="טלפון נייד" ltr value={customer.mobilePhone} /><Info label="טלפון בבית" ltr value={customer.homePhone} />
        <Info label="דוא״ל" ltr value={customer.email} /><Info label="מגדר" value={customer.gender === 'Male' ? 'זכר' : customer.gender === 'Female' ? 'נקבה' : customer.gender} />
        <Info label="עיר" value={customer.city} /><Info label="רחוב" value={customer.street} />
        <div className="sm:col-span-2 border-t pt-5"><Info label="הערות" value={customer.notes} /></div>
      </dl></CardContent></Card>
      <div className="space-y-5">
        <Card><CardHeader><CardTitle>העדפות תקשורת</CardTitle></CardHeader><CardContent>
          <label className="flex items-center gap-3 text-sm font-medium"><input type="checkbox" className="size-4 accent-primary" checked={customer.whatsAppConsent} disabled={busy} onChange={event => consent.mutate(event.target.checked)} />הסכמה להודעות WhatsApp</label>
          <p className="mt-2 text-xs text-muted-foreground">{consent.isPending ? 'שומר הסכמה…' : customer.whatsAppConsent ? 'ניתנה הסכמה' : 'לא ניתנה הסכמה'}</p>
          {consent.isError && <ErrorFeedback error={consent.error} />}
        </CardContent></Card>
        <Card><CardHeader><CardTitle>פעילות הרשומה</CardTitle></CardHeader><CardContent><dl className="space-y-4">
          <Info label="נוצר בתאריך" value={formatTimestamp(customer.createdAtUtc)} /><Info label="עודכן לאחרונה" value={customer.updatedAtUtc ? formatTimestamp(customer.updatedAtUtc) : 'טרם עודכן'} />
        </dl></CardContent></Card>
      </div>
    </div>
    <div className="mt-6 flex flex-wrap items-center justify-between gap-4 rounded-lg border bg-white p-5"><div><h2 className="text-sm font-semibold">סטטוס לקוח</h2><p className="mt-1 text-xs text-muted-foreground">{customer.isActive ? 'השבתת הלקוח אינה מוחקת אותו מהמערכת. הרשומה תישאר זמינה לצפייה ולחיפוש.' : 'לקוח זה אינו פעיל. הרשומה נשמרת במערכת ונשארת זמינה לצפייה ולחיפוש.'}</p></div>
      <Dialog open={confirm} onOpenChange={open => { if (!deactivate.isPending) setConfirm(open) }}>
        <DialogTrigger asChild><Button variant="destructive" disabled={!customer.isActive || busy}>השבתת לקוח</Button></DialogTrigger>
        <DialogContent><DialogHeader><DialogTitle>השבתת לקוח</DialogTitle><DialogDescription>האם להשבית את <bdi>{customer.firstName} {customer.lastName}</bdi>? הרשומה תישאר במערכת ותהיה זמינה לצפייה ולחיפוש.</DialogDescription></DialogHeader>
          {deactivate.isError && <ErrorFeedback error={deactivate.error} />}
          <DialogFooter><Button variant="outline" disabled={deactivate.isPending} onClick={() => setConfirm(false)}>ביטול</Button><Button variant="destructive" disabled={deactivate.isPending} onClick={() => deactivate.mutate()}>{deactivate.isPending ? 'משבית…' : 'אישור השבתה'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  </>
}
