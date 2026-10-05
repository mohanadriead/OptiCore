export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) { super(message); this.name = 'ApiError'; this.status = status }
}
type ProblemDetails = { title?: unknown; detail?: unknown; status?: number }
// Only recognized, safe validation text is surfaced. Unexpected server details never enter the UI.
function safeMessage(status: number, problem: ProblemDetails): string {
  if (status === 409) return 'A customer with this National ID already exists.'
  if (status === 404) return 'Customer not found. Check the customer number or return to search.'
  if (status === 400) {
    const title = typeof problem.title === 'string' ? problem.title : ''
    if (/^[a-zA-Z ]+ (is required\.|must be at most \d+ characters\.)$/.test(title)) return title
    return 'Please check the entered details and try again.'
  }
  return 'Unable to complete the request. Please try again.'
}
export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try { response = await fetch(path, options) } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error
    throw new ApiError(0, 'Cannot reach OptiCore. Check your connection and try again.')
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
    error.message.startsWith('Customer changes are unavailable') || error.message.startsWith('Enable browser storage')
  ) ? error.message : 'Unable to complete the request. Please try again.'
}
