import { z } from 'zod'
const required = (max: number) => z.string().trim().min(1, 'שדה זה הוא שדה חובה.').max(max, 'ניתן להזין עד ' + max + ' תווים.')
const optional = (max: number) => z.string().trim().max(max, 'ניתן להזין עד ' + max + ' תווים.')
const nationalId = z.string().trim().regex(/^[0-9]{9}$/, 'תעודת זהות חייבת להכיל בדיוק 9 ספרות.')
export const detailsSchema = z.object({
  firstName: required(100), lastName: required(100),
  dateOfBirth: z.string().refine(value => {
    if (!/^[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}$/.test(value) || value.startsWith('0000')) return false
    const iso = canonicalDate(value)
    const date = new Date(iso + 'T00:00:00Z')
    return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === iso
  }, 'יש להזין תאריך לידה תקין.').transform(canonicalDate),
  mobilePhone: z.string().trim().regex(/^[0-9]{10}$/, 'טלפון נייד חייב להכיל בדיוק 10 ספרות.'),
  homePhone: z.string().trim().refine(value => value === '' || /^[0-9]{9}$/.test(value), 'טלפון בבית חייב להכיל בדיוק 9 ספרות.'),
  email: optional(254),
  city: required(100), street: optional(200),
  gender: z.string().trim().refine((value): boolean => value === 'Male' || value === 'Female', 'יש לבחור מגדר.'), notes: optional(4000),
})
function canonicalDate(value: string) {
  const [year, month, day] = value.split('-')
  return `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`
}
export const createSchema = detailsSchema.extend({ nationalId, whatsAppConsent: z.boolean() })
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
