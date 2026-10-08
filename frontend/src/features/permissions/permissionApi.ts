import { apiRequest } from '@/lib/apiClient'
import { jsonRequest } from '../employees/employeeApi'

export const permissionCodes = ['ReceiveStock', 'GiveDiscount', 'ViewDailySales', 'ViewProfit', 'ViewSupplierDetails', 'ViewReports', 'ExportData', 'ViewAuditLogs'] as const
export type PermissionCode = typeof permissionCodes[number]
export interface Permission { code: PermissionCode; label: string }
export interface EmployeePermissions {
  employeeNumber: number
  isManager: boolean
  assignedPermissions: PermissionCode[]
  effectivePermissions: PermissionCode[]
}
export const permissionKeys = {
  catalog: ['permission-catalog'] as const,
  employee: (number: number) => ['employees', number, 'permissions'] as const,
  effective: ['effective-permissions'] as const,
}
export const getPermissionCatalog = (signal?: AbortSignal) => apiRequest<Permission[]>('/api/permissions', { signal, cache: 'no-store' })
export const getEmployeePermissions = (number: number, signal?: AbortSignal) => apiRequest<EmployeePermissions>(`/api/employees/${number}/permissions`, { signal, cache: 'no-store' })
export const setEmployeePermissions = (number: number, permissions: PermissionCode[]) => apiRequest<EmployeePermissions>(`/api/employees/${number}/permissions`, jsonRequest('PUT', { permissions }))
export const getEffectivePermissions = (signal?: AbortSignal) => apiRequest<PermissionCode[]>('/api/auth/permissions', { signal, cache: 'no-store' })
