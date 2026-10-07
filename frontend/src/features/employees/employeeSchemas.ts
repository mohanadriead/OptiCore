import { z } from 'zod'

const name = z.string().trim().min(1, 'שדה זה הוא שדה חובה.').max(100, 'ניתן להזין עד 100 תווים.')
export const passwordSchema = z.string().min(8, 'הסיסמה חייבת להכיל בין 8 ל־128 תווים.').max(128, 'הסיסמה חייבת להכיל בין 8 ל־128 תווים.').refine(value => value.trim().length > 0, 'יש להזין סיסמה שאינה רווחים בלבד.')
export const employeeSchema = z.object({
  firstName: name, lastName: name, username: name, password: passwordSchema,
  phone: z.string().trim().regex(/^[0-9]{10}$/, 'טלפון חייב להכיל בדיוק 10 ספרות.'),
  nationalId: z.string().trim().regex(/^[0-9]{9}$/, 'תעודת זהות חייבת להכיל בדיוק 9 ספרות.'),
  isManager: z.boolean(),
})
export const passwordChangeSchema = z.object({
  currentPassword: z.string(), newPassword: passwordSchema, confirmation: z.string(),
}).refine(value => value.newPassword === value.confirmation, { path: ['confirmation'], message: 'הסיסמאות אינן תואמות.' })
