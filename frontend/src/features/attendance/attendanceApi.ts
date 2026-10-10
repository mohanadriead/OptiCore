import { z } from 'zod'
import { ApiError, apiRequest } from '@/lib/apiClient'

export type AttendanceAction = 'check-in' | 'check-out'

export type AttendanceStatus = { employeeNumber: number; firstName: string; lastName: string; hasOpenAttendance: boolean; checkInAtUtc: string | null }
const attendanceTimestamp = z.iso.datetime({ offset: true })
const statusSchema = z.object({
  employeeNumber: z.number().int().positive().max(2147483647),
  firstName: z.string().trim().min(1), lastName: z.string().trim().min(1),
  hasOpenAttendance: z.boolean(), checkInAtUtc: attendanceTimestamp.nullable(),
}).refine(value => value.hasOpenAttendance === (value.checkInAtUtc !== null))
const actionSchema = z.object({
  employeeNumber: z.number().int().positive().max(2147483647),
  checkInAtUtc: attendanceTimestamp, checkOutAtUtc: attendanceTimestamp.nullable(),
}).refine(value => value.checkOutAtUtc === null || new Date(value.checkOutAtUtc) >= new Date(value.checkInAtUtc))
const invalidResponse = () => new ApiError(502, 'פרטי הנוכחות שהתקבלו אינם תקינים. יש לטעון מחדש.')
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

export async function getAttendanceStatus(employeeNumber: number, signal: AbortSignal): Promise<AttendanceStatus> {
  const response = await apiRequest<unknown>(`/api/attendance/${employeeNumber}/status`, { signal })
  const parsed = statusSchema.safeParse(response)
  if (!parsed.success || parsed.data.employeeNumber !== employeeNumber) throw invalidResponse()
  return parsed.data
}
export async function getMyAttendanceStatus(signal: AbortSignal): Promise<AttendanceStatus> {
  const parsed = statusSchema.safeParse(await apiRequest<unknown>('/api/attendance/me/status', { signal }))
  if (!parsed.success) throw invalidResponse()
  return parsed.data
}
export async function submitMyAttendance(action: AttendanceAction, signal: AbortSignal) {
  const parsed = actionSchema.safeParse(await apiRequest<unknown>(`/api/attendance/me/${action}`, { method: 'POST', signal }))
  if (!parsed.success || (action === 'check-in') !== (parsed.data.checkOutAtUtc === null)) throw invalidResponse()
  return parsed.data
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

export async function submitAttendance(employeeNumber: number, action: AttendanceAction, signal: AbortSignal) {
  // Retain only the subject number and attendance times; never pass audit identifiers to the UI.
  const response = await apiRequest<unknown>(`/api/attendance/${employeeNumber}/${action}`, { method: 'POST', signal })
  const parsed = actionSchema.safeParse(response)
  if (!parsed.success || parsed.data.employeeNumber !== employeeNumber ||
    (action === 'check-in') !== (parsed.data.checkOutAtUtc === null)) throw invalidResponse()
  return parsed.data
}
