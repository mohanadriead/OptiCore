export function validEmployeeNumber(value: string) {
  return /^[0-9]+$/.test(value) && Number.isInteger(Number(value)) && Number(value) > 0 && Number(value) <= 2147483647
}

export function attendanceTime(value: string | null) {
  return value ? new Date(value).toLocaleString('he-IL', { timeZone: 'Asia/Jerusalem' }) : '—'
}

export function israelInput(value: string | null): string {
  if (!value) return ''
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Jerusalem', year: 'numeric', month: '2-digit',
    day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value))
  const part = (name: Intl.DateTimeFormatPartTypes) => parts.find(p => p.type === name)!.value
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}:${part('second')}`
}

export function israelUtc(value: string, original: string | null): string | null {
  if (!value) return null
  // Preserve milliseconds and the original occurrence of a repeated winter-transition hour.
  if (original && value === israelInput(original)) return original
  const normalized = value.length === 16 ? value + ':00' : value
  for (const offset of ['+03:00', '+02:00']) {
    const date = new Date(normalized + offset)
    if (!Number.isNaN(date.getTime()) && israelInput(date.toISOString()) === normalized) return date.toISOString()
  }
  throw new Error('יש להזין זמן תקין לפי שעון ישראל.')
}
