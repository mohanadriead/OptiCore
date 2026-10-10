import { apiRequest } from '@/lib/apiClient'

export type AttendanceAction = 'check-in' | 'check-out'

export type AttendanceStatus = { employeeNumber: number; firstName: string; lastName: string; hasOpenAttendance: boolean; checkInAtUtc: string | null }
export type AttendanceRecord = {
  id: string; employeeNumber: number; firstName: string; lastName: string
  checkInAtUtc: string; checkOutAtUtc: string | null; wasCheckoutAutomatic: boolean
  automaticCheckoutDueAtUtc: string; checkoutProcessedAtUtc: string | null; updatedAtUtc: string | null
}
export type AttendanceCorrection = {
  previousCheckInAtUtc: string; previousCheckOutAtUtc: string | null; newCheckInAtUtc: string; newCheckOutAtUtc: string | null
  correctedAtUtc: string; correctedByEmployeeNumber: number; correctedByFirstName: string; correctedByLastName: string; reason: string
}
export type AttendanceDetails = { record: AttendanceRecord; corrections: AttendanceCorrection[] }
export type AttendanceFilters = { employeeNumber: string; from: string; to: string }
export type AttendanceHistory = { items: AttendanceRecord[]; total: number; page: number; pageSize: number }

export function getAttendanceStatus(employeeNumber: number, signal: AbortSignal) {
  return apiRequest<AttendanceStatus>(`/api/attendance/${employeeNumber}/status`, { signal })
}
export function getAttendanceHistory(filters: AttendanceFilters, page: number, signal: AbortSignal) {
  const query = new URLSearchParams({ page: String(page), pageSize: '25' })
  for (const [key, value] of Object.entries(filters)) if (value) query.set(key, value)
  return apiRequest<AttendanceHistory>(`/api/attendance/management/records?${query}`, { signal })
}
export function getAttendanceDetails(id: string, signal: AbortSignal) {
  return apiRequest<AttendanceDetails>(`/api/attendance/management/records/${id}`, { signal })
}
export function correctAttendance(record: AttendanceRecord, checkInAtUtc: string, checkOutAtUtc: string | null, reason: string, signal: AbortSignal) {
  return apiRequest<AttendanceDetails>(`/api/attendance/management/records/${record.id}/corrections`, {
    method: 'POST', signal, headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ checkInAtUtc, checkOutAtUtc, reason, expectedCheckInAtUtc: record.checkInAtUtc,
      expectedCheckOutAtUtc: record.checkOutAtUtc, expectedUpdatedAtUtc: record.updatedAtUtc }),
  })
}

export async function submitAttendance(employeeNumber: number, action: AttendanceAction, signal: AbortSignal): Promise<void> {
  // Attendance records contain identifiers and audit data; the UI needs only success/failure.
  await apiRequest<unknown>(`/api/attendance/${employeeNumber}/${action}`, { method: 'POST', signal })
}
