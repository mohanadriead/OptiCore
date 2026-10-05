import { z } from 'zod'
const required = (max: number) => z.string().trim().min(1, 'This field is required.').max(max, 'Use at most ' + max + ' characters.')
const optional = (max: number) => z.string().trim().max(max, 'Use at most ' + max + ' characters.')
export const detailsSchema = z.object({
  firstName: required(100), lastName: required(100),
  dateOfBirth: z.string().min(1, 'This field is required.').refine(value => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith('0000')) return false
    const date = new Date(value + 'T00:00:00Z')
    return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value
  }, 'Enter a valid date.'),
  mobilePhone: required(30), homePhone: optional(30), email: optional(254),
  city: required(100), street: optional(200), gender: required(50), notes: optional(4000),
})
export const createSchema = detailsSchema.extend({ nationalId: required(9), whatsAppConsent: z.boolean() })
export type CustomerFormValues = z.infer<typeof createSchema>
export const emptyValues: CustomerFormValues = {
  nationalId: '', firstName: '', lastName: '', dateOfBirth: '', mobilePhone: '', homePhone: '',
  email: '', city: '', street: '', gender: '', notes: '', whatsAppConsent: false,
}
export function detailsPayload(values: CustomerFormValues) {
  const details = detailsSchema.parse(values)
  return { ...details, homePhone: details.homePhone || null, email: details.email || null,
    street: details.street || null, notes: details.notes || null }
}
