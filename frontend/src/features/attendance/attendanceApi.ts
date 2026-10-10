import { apiRequest } from '@/lib/apiClient'

export type AttendanceAction = 'check-in' | 'check-out'

export async function submitAttendance(employeeNumber: number, action: AttendanceAction, signal: AbortSignal): Promise<void> {
  // Attendance records contain identifiers and audit data; the UI needs only success/failure.
  await apiRequest<unknown>(`/api/attendance/${employeeNumber}/${action}`, { method: 'POST', signal })
}
