import { useEffect, useRef } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { Glasses, Users, LayoutDashboard, ShoppingBag, Package, Eye, UserRound, ChartNoAxesCombined, Settings } from 'lucide-react'
import { appDirection } from '@/app/direction'
const future = [
  { label: 'Dashboard', icon: LayoutDashboard }, { label: 'Orders', icon: ShoppingBag },
  { label: 'Inventory', icon: Package }, { label: 'Eye Exams', icon: Eye },
  { label: 'Employees', icon: UserRound }, { label: 'Reports', icon: ChartNoAxesCombined }, { label: 'Settings', icon: Settings },
]
export function AppShell() {
  const location = useLocation()
  const main = useRef<HTMLElement>(null)
  useEffect(() => { main.current?.focus(); window.scrollTo(0, 0) }, [location.pathname])
  return <div dir={appDirection} className="min-h-screen">
    <a className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-white focus:p-3" href="#main">Skip to content</a>
    <header className="flex h-16 items-center justify-between border-b bg-white px-6">
      <NavLink to="/customers" className="flex items-center gap-2.5 text-xl font-semibold tracking-tight"><span className="rounded-lg bg-primary p-1.5 text-white"><Glasses size={24} /></span>OptiCore</NavLink>
      <span className="rounded-full border px-3 py-1 text-xs font-medium text-muted-foreground">{import.meta.env.DEV ? 'Development' : 'Preview · read only'}</span>
    </header>
    <div className="flex min-h-[calc(100vh-4rem)] flex-col md:flex-row">
      <aside className="border-b bg-white p-4 md:w-56 md:shrink-0 md:border-e md:border-b-0">
        <p className="mb-3 px-3 text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground">Workspace</p>
        <nav aria-label="Main navigation" className="flex flex-wrap gap-1 md:flex-col">
          <NavLink to="/customers" className="flex items-center gap-3 rounded-md bg-accent px-3 py-2.5 text-sm font-semibold text-primary"><Users size={18} />Customers</NavLink>
          {future.map(({ label, icon: Icon }) => <button key={label} disabled className="flex items-center gap-3 px-3 py-2.5 text-start text-sm text-slate-400" title="Not available yet"><Icon size={18} />{label}<span className="ms-auto text-[10px]">Soon</span></button>)}
        </nav>
      </aside>
      <main id="main" ref={main} tabIndex={-1} className="min-w-0 flex-1 p-5 outline-none md:p-8"><div className="mx-auto max-w-6xl"><Outlet /></div></main>
    </div>
  </div>
}
