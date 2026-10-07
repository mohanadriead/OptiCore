import { apiRequest } from '@/lib/apiClient'

export interface Employee {
  id: string
  employeeNumber: number
  firstName: string
  lastName: string
  username: string
  phone: string
  nationalId: string
  isActive: boolean
  isManager: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}
export interface CreateEmployeeRequest {
  firstName: string
  lastName: string
  username: string
  password: string
  phone: string
  nationalId: string
  isManager: boolean
}
export function jsonRequest(method: string, body?: unknown): RequestInit {
  return { method, headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) }
}
const base = '/api/employees'
export const listEmployees = (signal?: AbortSignal) => apiRequest<Employee[]>(base, { signal })
export const createEmployee = (body: CreateEmployeeRequest) => apiRequest<Employee>(base, jsonRequest('POST', body))
export const deactivateEmployee = (number: number) => apiRequest<void>(`${base}/${number}/deactivate`, jsonRequest('PATCH'))
export const setManagerStatus = (number: number, isManager: boolean) => apiRequest<Employee>(`${base}/${number}/manager-status`, jsonRequest('PATCH', { isManager }))
export const resetEmployeePassword = (number: number, newPassword: string) => apiRequest<void>(`${base}/${number}/reset-password`, jsonRequest('POST', { newPassword }))
