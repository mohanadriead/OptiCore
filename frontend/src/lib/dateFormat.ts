// Date-only values are formatted in UTC to preserve their calendar date.
export function formatDateOnly(value: string): string {
  return new Intl.DateTimeFormat('he-IL', { timeZone: 'UTC' }).format(new Date(value + 'T00:00:00Z'))
}

export function formatTimestamp(value: string): string {
  return new Date(value).toLocaleString('he-IL')
}
