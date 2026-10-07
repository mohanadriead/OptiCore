import { ApiError, apiRequest } from '@/lib/apiClient'
import { jsonRequest, type Employee } from '../employees/employeeApi'

export const getCurrentEmployee = (signal?: AbortSignal) => apiRequest<Employee>('/api/auth/me', { signal, cache: 'no-store' })
export const login = (username: string, password: string) => apiRequest<Employee>('/api/auth/login', jsonRequest('POST', { username, password }), false)
export const logout = () => apiRequest<void>('/api/auth/logout', jsonRequest('POST'))
export async function changeOwnPassword(currentPassword: string, newPassword: string) {
  try {
    await apiRequest<void>('/api/auth/change-password', jsonRequest('POST', { currentPassword, newPassword }), false)
  } catch (error) {
    // This endpoint also uses 401 for an incorrect current password. Only /me can
    // distinguish that from an expired cookie without changing the backend contract.
    if (error instanceof ApiError && error.status === 401) {
      await getCurrentEmployee()
      throw new ApiError(401, 'הסיסמה הנוכחית שגויה.')
    }
    throw error
  }
}
