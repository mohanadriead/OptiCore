import { LoaderCircle } from 'lucide-react'
import { ApiError, errorMessage } from '@/lib/apiClient'
export function Loading() { return <p role="status" className="flex items-center gap-2 py-10 text-sm text-muted-foreground"><LoaderCircle className="animate-spin" size={18} />Loading customer information…</p> }
export function ErrorFeedback({ error }: { error: unknown }) { return <div role="alert" className="my-4 rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">{error instanceof ApiError && error.status === 404 ? 'Customer not found. Return to search to find another customer.' : errorMessage(error)}</div> }
