import { invalidateSession, sessionRevision } from './sessionEvents'

export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) { super(message); this.name = 'ApiError'; this.status = status }
}
type ProblemDetails = { title?: unknown; detail?: unknown; status?: number }
const validationFields: Record<string, string> = {
  nationalid: 'תעודת זהות', firstname: 'שם פרטי', lastname: 'שם משפחה',
  mobilephone: 'טלפון נייד', homephone: 'טלפון בבית', email: 'דוא״ל',
  city: 'עיר', street: 'רחוב', gender: 'מגדר', notes: 'הערות', username: 'שם משתמש', phone: 'טלפון',
}
const knownErrors = new Map<string, string>([
  ['409:A customer with this NationalId already exists.', 'לקוח עם תעודת זהות זו כבר קיים במערכת.'],
  ['404:Customer was not found.', 'הלקוח לא נמצא. בדוק את מספר הלקוח או חזור לחיפוש.'],
  ['404:Employee was not found.', 'העובד לא נמצא.'],
  ['409:Username already exists.', 'שם המשתמש כבר קיים במערכת.'],
  ['409:An employee with this NationalId already exists.', 'עובד עם תעודת זהות זו כבר קיים במערכת.'],
  ['409:At least one active manager must remain.', 'חייב להישאר לפחות מנהל פעיל אחד.'],
  ['401:Invalid username or password.', 'שם המשתמש או הסיסמה שגויים.'],
  ['403:Employee is inactive.', 'העובד אינו פעיל.'],
  ['403:Manager authorization is required.', 'נדרשת הרשאת מנהל.'],
])
// Only recognized, safe validation text is surfaced. Unexpected server details never enter the UI.
function safeMessage(status: number, problem: ProblemDetails): string {
  const title = typeof problem.title === 'string' ? problem.title : ''
  const knownMessage = knownErrors.get(`${status}:${title}`)
  if (knownMessage) return knownMessage
  if (status === 400) {
    const formatMessages: Record<string, string> = {
      'National ID must contain exactly 9 ASCII digits.': 'תעודת זהות חייבת להכיל בדיוק 9 ספרות.',
      'Mobile phone must contain exactly 10 ASCII digits.': 'טלפון נייד חייב להכיל בדיוק 10 ספרות.',
      'Home phone must contain exactly 9 ASCII digits.': 'טלפון בבית חייב להכיל בדיוק 9 ספרות.',
      'Gender must be Male or Female.': 'יש לבחור מגדר.',
      'Phone must contain exactly 10 ASCII digits.': 'טלפון חייב להכיל בדיוק 10 ספרות.',
      'Password must be between 8 and 128 characters.': 'הסיסמה חייבת להכיל בין 8 ל־128 תווים.',
    }
    if (Object.hasOwn(formatMessages, title)) return formatMessages[title]
    const match = /^([a-zA-Z ]+) (is required\.|must be at most (\d+) characters\.)$/.exec(title)
    const key = match?.[1].replaceAll(' ', '').toLowerCase()
    const field = key && Object.hasOwn(validationFields, key) ? validationFields[key] : undefined
    if (field && match) return match[3] ? field + ': ניתן להזין עד ' + match[3] + ' תווים.' : field + ': שדה זה הוא שדה חובה.'
  }
  return 'לא ניתן להשלים את הפעולה. נסה שוב.'
}
export async function apiRequest<T>(path: string, options: RequestInit = {}, invalidateOn401 = true): Promise<T> {
  const revision = sessionRevision()
  const ensureCurrentSession = () => {
    if (revision !== sessionRevision() || options.signal?.aborted) throw new DOMException('Request cancelled', 'AbortError')
  }
  let response: Response
  try { response = await fetch(path, { ...options, credentials: 'same-origin' }) } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error
    throw new ApiError(0, 'לא ניתן להתחבר ל-OptiCore. בדוק את החיבור ונסה שוב.')
  }
  // Late responses from a previous login must not refill caches or invalidate a new session.
  ensureCurrentSession()
  if (!response.ok) {
    if (response.status === 401 && invalidateOn401 && !options.signal?.aborted) invalidateSession()
    const problem: ProblemDetails = await response.json().catch(() => ({}))
    throw new ApiError(response.status, safeMessage(response.status, problem ?? {}))
  }
  if (response.status === 204) return undefined as T
  const data = await response.json() as T
  ensureCurrentSession()
  return data
}
export function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : 'לא ניתן להשלים את הפעולה. נסה שוב.'
}
