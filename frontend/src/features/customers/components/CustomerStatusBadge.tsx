import { Badge } from '@/components/ui/badge'
export function CustomerStatusBadge({ active }: { active: boolean }) {
  return <Badge variant="outline" className={active ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'bg-slate-100 text-slate-600'}>{active ? 'Active' : 'Inactive'}</Badge>
}
