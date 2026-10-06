export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) { super(message); this.name = 'ApiError'; this.status = status }
}
type ProblemDetails = { title?: unknown; detail?: unknown; status?: number }
const validationFields: Record<string, string> = {
  nationalid: 'תעודת זהות', firstname: 'שם פרטי', lastname: 'שם משפחה',
  mobilephone: 'טלפון נייד', homephone: 'טלפון בבית', email: 'דוא״ל',
  city: 'עיר', street: 'רחוב', gender: 'מגדר', notes: 'הערות',
}
// Only recognized, safe validation text is surfaced. Unexpected server details never enter the UI.
function safeMessage(status: number, problem: ProblemDetails): string {
  if (status === 409) return 'לקוח עם תעודת זהות זו כבר קיים במערכת.'
  if (status === 404) return 'הלקוח לא נמצא. בדוק את מספר הלקוח או חזור לחיפוש.'
  if (status === 400) {
    const title = typeof problem.title === 'string' ? problem.title : ''
    const match = /^([a-zA-Z ]+) (is required\.|must be at most (\d+) characters\.)$/.exec(title)
    const field = match && validationFields[match[1].replaceAll(' ', '').toLowerCase()]
    if (field) return match[3] ? field + ': ניתן להזין עד ' + match[3] + ' תווים.' : field + ': שדה זה הוא שדה חובה.'
    return 'בדוק את הפרטים שהוזנו ונסה שוב.'
  }
  return 'לא ניתן להשלים את הפעולה. נסה שוב.'
}
export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try { response = await fetch(path, options) } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error
    throw new ApiError(0, 'לא ניתן להתחבר ל-OptiCore. בדוק את החיבור ונסה שוב.')
  }
  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}))
    throw new ApiError(response.status, safeMessage(response.status, problem ?? {}))
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}
export function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : error instanceof Error && (
    error.message === 'לא ניתן לשמור שינויים בלקוחות עד להגדרת הזדהות.' || error.message === 'יש לאפשר אחסון בדפדפן כדי לשמור שינויים בלקוחות בסביבת הפיתוח.'
  ) ? error.message : 'לא ניתן להשלים את הפעולה. נסה שוב.'
}
