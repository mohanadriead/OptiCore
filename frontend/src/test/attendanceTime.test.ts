import { expect, it } from 'vitest'
import { israelInput, israelUtc } from '@/features/attendance/attendanceTime'

it.each([
  ['2026-01-01T10:00:00Z', '2026-01-01T12:00:00'],
  ['2026-07-01T10:00:00Z', '2026-07-01T13:00:00'],
])('converts Israel correction times independently of browser timezone: %s', (utc, local) => {
  expect(israelInput(utc)).toBe(local)
  expect(israelUtc(local, null)).toBe(new Date(utc).toISOString())
})
it('preserves exact timestamps when inputs are unchanged', () => {
  const original = '2026-10-24T23:30:00.123Z'
  expect(israelUtc(israelInput(original), original)).toBe(original)
})
it('rejects nonexistent Israel spring-transition times', () => {
  expect(() => israelUtc('2026-03-27T02:30', null)).toThrow()
  expect(israelUtc('', null)).toBeNull()
})
it('chooses the first occurrence for a new repeated winter-transition time', () => {
  expect(israelUtc('2026-10-25T01:30', null)).toBe('2026-10-24T22:30:00.000Z')
})
