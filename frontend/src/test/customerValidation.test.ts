import { expect, it } from 'vitest'
import { createSchema, detailsSchema, detailsPayload, emptyValues } from '@/features/customers/schemas/customerSchemas'

it.each(['letters', '1'.repeat(8), '1'.repeat(10), '1'.repeat(8) + 'A', '١'.repeat(9), '１'.repeat(9)])('rejects invalid national ID case %#', value => {
  expect(createSchema.shape.nationalId.safeParse(value).success).toBe(false)
})
it.each(['1'.repeat(9), '1'.repeat(11), '1'.repeat(9) + 'A', '١'.repeat(10), '1'.repeat(5) + '-' + '1'.repeat(4)])('rejects invalid mobile case %#', value => {
  expect(detailsSchema.shape.mobilePhone.safeParse(value).success).toBe(false)
})
it.each(['1'.repeat(8), '1'.repeat(10), '1'.repeat(8) + 'A', '١'.repeat(9)])('rejects invalid home phone case %#', value => {
  expect(detailsSchema.shape.homePhone.safeParse(value).success).toBe(false)
})
it.each(['', '   ', '0'.repeat(9)])('accepts blank or valid home phone case %#', value => {
  expect(detailsSchema.shape.homePhone.safeParse(value).success).toBe(true)
})
it('retains leading zeros and existing trimming/blank-to-null behavior', () => {
  expect(createSchema.shape.nationalId.parse(' ' + '0'.repeat(9) + ' ')).toBe('0'.repeat(9))
  expect(detailsSchema.shape.mobilePhone.parse(' ' + '0'.repeat(10) + ' ')).toBe('0'.repeat(10))
  expect(detailsPayload({ ...emptyValues, firstName: 'First', lastName: 'Last', mobilePhone: '0'.repeat(10), city: 'City',
    gender: 'Male', dateOfBirth: '2000-1-2', homePhone: ' ' })).toMatchObject({ homePhone: null, dateOfBirth: '2000-01-02' })
})
it.each(['', 'Other', 'male', 'זכר'])('rejects noncanonical gender case %#', gender => {
  expect(detailsSchema.shape.gender.safeParse(gender).success).toBe(false)
})
it.each(['2023-2-29', '2000-2-30', '1900-2-29', '2000-13-1', '2000-0-1', '2000-1-0', '0000-1-1', '200-1-1', '2000--1', '--', ''])('rejects invalid calendar date case %#', date => {
  expect(detailsSchema.shape.dateOfBirth.safeParse(date).success).toBe(false)
})
it.each([['2000-2-29', '2000-02-29'], ['0001-1-1', '0001-01-01'], ['2024-12-31', '2024-12-31']])('preserves valid date-only case %#', (date, expected) => {
  expect(detailsSchema.shape.dateOfBirth.parse(date)).toBe(expected)
})
