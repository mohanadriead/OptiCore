import { useEffect, useRef } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { Glasses, Users, LayoutDashboard, ShoppingBag, Package, Eye, UserRound, ChartNoAxesCombined, Settings, Clock } from 'lucide-react'
import { appDirection } from '@/app/direction'
import { useAuth } from '@/features/auth/authContext'
import { CurrentEmployeeMenu } from '@/features/auth/CurrentEmployeeMenu'
const future = [
  { label: 'לוח בקרה', icon: LayoutDashboard }, { label: 'הזמנות', icon: ShoppingBag },
  { label: 'מלאי', icon: Package }, { label: 'בדיקות ראייה', icon: Eye },
  { label: 'דוחות', icon: ChartNoAxesCombined }, { label: 'הגדרות', icon: Settings },
]
export function AppShell() {
  const auth = useAuth()
  const location = useLocation()
  const main = useRef<HTMLElement>(null)
  useEffect(() => { main.current?.focus(); window.scrollTo(0, 0) }, [location.pathname])
  return <div dir={appDirection} className="min-h-screen">
    <a className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-white focus:p-3" href="#main">דילוג לתוכן</a>
    <header className="flex min-h-16 flex-wrap items-center justify-between gap-4 border-b bg-white px-6 py-3">
      <NavLink to="/customers" className="flex items-center gap-2.5 text-xl font-semibold tracking-tight"><span className="rounded-lg bg-primary p-1.5 text-white"><Glasses size={24} /></span>OptiCore</NavLink>
      <CurrentEmployeeMenu />
    </header>
    <div className="flex min-h-[calc(100vh-4rem)] flex-col md:flex-row">
      <aside className="border-b bg-white p-4 md:w-56 md:shrink-0 md:border-e md:border-b-0">
        <p className="mb-3 px-3 text-[10px] font-bold tracking-normal text-muted-foreground">סביבת עבודה</p>
        <nav aria-label="ניווט ראשי" className="flex flex-wrap gap-1 md:flex-col">
          <NavLink to="/customers" className={({ isActive }) => `flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-semibold text-primary ${isActive ? 'bg-accent' : ''}`}><Users size={18} />לקוחות</NavLink>
          {auth.employee?.isManager && <NavLink to="/employees" className={({ isActive }) => `flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-semibold text-primary ${isActive ? 'bg-accent' : ''}`}><UserRound size={18} />עובדים</NavLink>}
          {auth.employee?.isManager && <NavLink to="/attendance" className={({ isActive }) => `flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-semibold text-primary ${isActive ? 'bg-accent' : ''}`}><Clock size={18} aria-hidden="true" />נוכחות</NavLink>}
          {future.map(({ label, icon: Icon }) => <button key={label} disabled className="flex items-center gap-3 px-3 py-2.5 text-start text-sm text-slate-400" title="עדיין לא זמין"><Icon size={18} />{label}<span className="ms-auto text-[10px]">בקרוב</span></button>)}
        </nav>
      </aside>
      <main id="main" ref={main} tabIndex={-1} className="min-w-0 flex-1 p-5 outline-none md:p-8"><div className="mx-auto max-w-6xl"><Outlet /></div></main>
    </div>
  </div>
}
