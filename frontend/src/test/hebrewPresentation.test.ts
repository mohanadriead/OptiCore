import { expect, it, vi } from 'vitest'
import { formatDateOnly, formatTimestamp } from '@/lib/dateFormat'
import { apiRequest } from '@/lib/apiClient'
import { detailsSchema } from '@/features/customers/schemas/customerSchemas'

it('formats a date-only value for Israel without shifting its calendar day', () => {
  expect(formatDateOnly('1995-04-20')).toBe('20.4.1995')
  expect(formatDateOnly('2000-01-01')).toBe('1.1.2000')
  const timestamp = '2026-01-01T10:00:00Z'
  expect(formatTimestamp(timestamp)).toBe(new Date(timestamp).toLocaleString('he-IL'))
})

it('translates optional-field length validation without changing the limit', () => {
  const result = detailsSchema.shape.email.safeParse('x'.repeat(255))
  expect(result.success).toBe(false)
  if (!result.success) expect(result.error.issues[0].message).toBe('ניתן להזין עד 254 תווים.')
})

it.each([
  ['First name is required.', 'שם פרטי: שדה זה הוא שדה חובה.'],
  ['email must be at most 254 characters.', 'דוא״ל: ניתן להזין עד 254 תווים.'],
  ['Unexpected database detail', 'לא ניתן להשלים את הפעולה. נסה שוב.'],
  ['National ID must contain exactly 9 ASCII digits.', 'תעודת זהות חייבת להכיל בדיוק 9 ספרות.'],
  ['Mobile phone must contain exactly 10 ASCII digits.', 'טלפון נייד חייב להכיל בדיוק 10 ספרות.'],
  ['Home phone must contain exactly 9 ASCII digits.', 'טלפון בבית חייב להכיל בדיוק 9 ספרות.'],
  ['Gender must be Male or Female.', 'יש לבחור מגדר.'],
])('translates or safely replaces backend validation: %s', async (title, message) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title }), { status: 400 })))
  await expect(apiRequest('/api/customers')).rejects.toMatchObject({ status: 400, message })
})
