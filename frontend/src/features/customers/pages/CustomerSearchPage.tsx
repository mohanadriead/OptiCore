import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Search, Plus, Users } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table'
import { searchCustomers } from '../api/customerApi'
import { customerKeys } from '../api/customerQueries'
import { CustomerStatusBadge } from '../components/CustomerStatusBadge'
import { Loading, ErrorFeedback } from '../components/Feedback'

export function CustomerSearchPage() {
  const [params, setParams] = useSearchParams()
  const query = (params.get('q') ?? '').trim()
  const [input, setInput] = useState(query)
  const navigate = useNavigate()
  const result = useQuery({ queryKey: customerKeys.search(query), queryFn: ({ signal }) => searchCustomers(query, signal), enabled: !!query })
  return <>
    <div className="mb-7 flex flex-wrap items-center justify-between gap-4"><div><h1>לקוחות</h1><p className="mt-1 text-sm text-muted-foreground">חיפוש לקוח וניהול פרטיו.</p></div>
      <Button asChild><Link to="/customers/new"><Plus size={16} />לקוח חדש</Link></Button></div>
    <section className="overflow-hidden rounded-lg border bg-white">
      <form onSubmit={event => { event.preventDefault(); if (input.trim() === query && query) void result.refetch(); else setParams(input.trim() ? { q: input.trim() } : {}) }} className="border-b p-5">
        <label htmlFor="customer-search" className="mb-2 block text-sm font-medium">חיפוש לקוחות</label>
        <div className="flex gap-3"><Input id="customer-search" value={input} onChange={event => setInput(event.target.value)} placeholder="שם, מספר לקוח, תעודת זהות, טלפון או דוא״ל" className="max-w-xl" /><Button type="submit" disabled={result.isFetching}><Search size={16} />חיפוש</Button></div>
        <p className="mt-2 text-xs text-muted-foreground">החיפוש כולל לקוחות פעילים ולא פעילים.</p>
      </form>
      {!query ? <div className="flex flex-col items-center px-6 py-20 text-center"><Users className="mb-4 text-slate-400" size={32} /><h2 className="font-semibold">איתור לקוח</h2><p className="mt-2 max-w-sm text-sm text-muted-foreground">הזן שם, מספר או פרטי קשר לחיפוש ברשומות הלקוחות.</p></div> :
        result.isPending ? <div className="px-6"><Loading /></div> : result.isError ? <div className="px-5"><ErrorFeedback error={result.error} /></div> :
        !result.data?.length ? <p role="status" className="p-12 text-center text-sm text-muted-foreground">לא נמצאו לקוחות. נסה שם או פרטי קשר אחרים.</p> :
        <><div className="flex items-center justify-between px-5 py-3 text-xs text-muted-foreground"><span>נמצאו {result.data.length} לקוחות</span>{result.isFetching && <span role="status">מרענן…</span>}</div>
          <Table><TableHeader><TableRow>{['מספר לקוח', 'שם מלא', 'תעודת זהות', 'טלפון נייד', 'עיר', 'סטטוס'].map(label => <TableHead key={label} className="text-start">{label}</TableHead>)}</TableRow></TableHeader>
            <TableBody>{result.data.map(customer => <TableRow key={customer.id} onClick={() => navigate('/customers/' + customer.customerNumber)} className="cursor-pointer">
              <TableCell><Link className="font-medium text-primary underline-offset-4 hover:underline" to={'/customers/' + customer.customerNumber}><bdi dir="ltr">#{customer.customerNumber}</bdi></Link></TableCell>
              <TableCell className="font-medium">{customer.firstName} {customer.lastName}</TableCell><TableCell><bdi dir="ltr">{customer.nationalId}</bdi></TableCell><TableCell><bdi dir="ltr">{customer.mobilePhone}</bdi></TableCell><TableCell>{customer.city}</TableCell><TableCell><CustomerStatusBadge active={customer.isActive} /></TableCell>
            </TableRow>)}</TableBody></Table></>}
    </section>
  </>
}
