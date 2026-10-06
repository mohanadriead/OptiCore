import { createBrowserRouter, Navigate } from 'react-router'
import { AppShell } from '@/components/layout/AppShell'
import { CustomerSearchPage } from '@/features/customers/pages/CustomerSearchPage'
import { CustomerCreatePage } from '@/features/customers/pages/CustomerCreatePage'
import { CustomerEditPage } from '@/features/customers/pages/CustomerEditPage'
import { CustomerDetailsPage } from '@/features/customers/pages/CustomerDetailsPage'
export const routes = [{ element: <AppShell />, children: [
  { path: '/', element: <Navigate to="/customers" replace /> },
  { path: '/customers', element: <CustomerSearchPage /> },
  { path: '/customers/new', element: <CustomerCreatePage /> },
  { path: '/customers/:customerNumber', element: <CustomerDetailsPage /> },
  { path: '/customers/:customerNumber/edit', element: <CustomerEditPage /> },
  { path: '*', element: <div><h1>העמוד לא נמצא</h1><a href="/customers">חזרה ללקוחות</a></div> },
]}]
export const router = createBrowserRouter(routes)
