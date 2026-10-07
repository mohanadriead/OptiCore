import { expect, it } from 'vitest'
import { employeeSchema, passwordSchema } from '@/features/employees/employeeSchemas'

const valid = () => ({ firstName: ' Test ', lastName: ' Person ', username: ' test ', password: 'x'.repeat(8), phone: '0000000000', nationalId: '000000000', isManager: false })
it('trims required text and preserves identifier zeros and password whitespace', () => {
  const input = { ...valid(), password: ' ' + 'x'.repeat(8) + ' ' }
  const result = employeeSchema.parse(input)
  expect(result.firstName).toBe('Test'); expect(result.username).toBe('test')
  expect(result.nationalId).toBe(input.nationalId); expect(result.phone).toBe(input.phone)
  expect(result.password === input.password).toBe(true)
})
it.each(['firstName', 'lastName', 'username'] as const)('requires trimmed %s, maximum 100 characters', field => {
  expect(employeeSchema.safeParse({ ...valid(), [field]: ' '.repeat(4) }).success).toBe(false)
  expect(employeeSchema.safeParse({ ...valid(), [field]: 'x'.repeat(101) }).success).toBe(false)
  expect(employeeSchema.safeParse({ ...valid(), [field]: 'x'.repeat(100) }).success).toBe(true)
})
it.each(['12345678', '1234567890', '１２３４５６７８９', '٠٠٠٠٠٠٠٠٠', '000-00000'])('rejects malformed national ID %#', nationalId => {
  expect(employeeSchema.safeParse({ ...valid(), nationalId }).success).toBe(false)
})
it.each(['123456789', '12345678901', '１２３４５６７８９０', '٠٠٠٠٠٠٠٠٠٠', '000-000000'])('rejects malformed phone %#', phone => {
  expect(employeeSchema.safeParse({ ...valid(), phone }).success).toBe(false)
})
it.each([0, 7, 129])('rejects password length %i', length => {
  expect(passwordSchema.safeParse('x'.repeat(length)).success).toBe(false)
})
it.each([8, 128])('accepts password length %i without invented complexity rules', length => {
  expect(passwordSchema.safeParse('x'.repeat(length)).success).toBe(true)
})
it('rejects whitespace-only password', () => {
  expect(passwordSchema.safeParse(' '.repeat(8)).success).toBe(false)
})
it.each(['firstName', 'lastName', 'username', 'password', 'phone', 'nationalId'] as const)('schema independently rejects an empty %s', field => {
  const result = employeeSchema.safeParse({ ...valid(), [field]: '' })
  expect(result.success).toBe(false)
  if (!result.success) {
    expect(result.error.issues[0].path).toEqual([field])
    expect(result.error.issues[0].message).toMatch(/[א-ת]/)
  }
})
it.each(['phone', 'nationalId'] as const)('schema rejects programmatic alphabetic and numeric %s values', field => {
  expect(employeeSchema.safeParse({ ...valid(), [field]: 'abcdefghi' }).success).toBe(false)
  expect(employeeSchema.safeParse({ ...valid(), [field]: 123456789 }).success).toBe(false)
})
