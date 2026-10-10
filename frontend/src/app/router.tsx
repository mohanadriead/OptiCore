import { createBrowserRouter, Navigate } from 'react-router'
import { AppShell } from '@/components/layout/AppShell'
import { CustomerSearchPage } from '@/features/customers/pages/CustomerSearchPage'
import { CustomerCreatePage } from '@/features/customers/pages/CustomerCreatePage'
import { CustomerEditPage } from '@/features/customers/pages/CustomerEditPage'
import { CustomerDetailsPage } from '@/features/customers/pages/CustomerDetailsPage'
import { RequireEmployee, RequireManager } from '@/features/auth/RouteGuards'
import { LoginPage } from '@/features/auth/LoginPage'
import { ChangePasswordPage } from '@/features/auth/ChangePasswordPage'
import { EmployeesPage } from '@/features/employees/EmployeesPage'
import { EmployeeCreatePage } from '@/features/employees/EmployeeCreatePage'
import { AttendancePage } from '@/features/attendance/AttendancePage'
import { AttendanceManagementPage } from '@/features/attendance/AttendanceManagementPage'
export const routes = [{ path: '/login', element: <LoginPage /> }, { element: <RequireEmployee />, children: [{ element: <AppShell />, children: [
  { path: '/', element: <Navigate to="/customers" replace /> },
  { path: '/customers', element: <CustomerSearchPage /> },
  { path: '/customers/new', element: <CustomerCreatePage /> },
  { path: '/customers/:customerNumber', element: <CustomerDetailsPage /> },
  { path: '/customers/:customerNumber/edit', element: <CustomerEditPage /> },
  { path: '/change-password', element: <ChangePasswordPage /> },
  { path: '/attendance', element: <AttendancePage /> },
  { element: <RequireManager />, children: [
    { path: '/attendance/management', element: <AttendanceManagementPage /> },
    { path: '/employees', element: <EmployeesPage /> },
    { path: '/employees/new', element: <EmployeeCreatePage /> },
  ] },
  { path: '*', element: <div><h1>העמוד לא נמצא</h1><a href="/customers">חזרה ללקוחות</a></div> },
]}]}]
export const router = createBrowserRouter(routes)
