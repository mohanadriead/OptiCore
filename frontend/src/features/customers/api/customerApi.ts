import { apiRequest } from '@/lib/apiClient'
import type { Customer, CreateCustomerRequest, UpdateCustomerRequest } from '../types/customer'

const base = '/api/customers'
function mutate<T>(path: string, method: string, body?: unknown, signal?: AbortSignal) {
  return apiRequest<T>(path, { method, signal, headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body) })
}
export const createCustomer = (body: CreateCustomerRequest, signal?: AbortSignal) => mutate<Customer>(base, 'POST', body, signal)
export const getCustomerByNumber = (number: number, signal?: AbortSignal) => apiRequest<Customer>(base + '/' + number, { signal })
export const getCustomerByNationalId = (id: string, signal?: AbortSignal) => apiRequest<Customer>(base + '/by-national-id/' + encodeURIComponent(id), { signal })
export const searchCustomers = (query: string, signal?: AbortSignal) => {
  if (!query.trim()) return Promise.resolve<Customer[]>([])
  return apiRequest<Customer[]>(base + '/search?q=' + encodeURIComponent(query.trim()), { signal })
}
export const updateCustomer = (number: number, body: UpdateCustomerRequest, signal?: AbortSignal) => mutate<Customer>(base + '/' + number, 'PUT', body, signal)
export const setWhatsAppConsent = (number: number, consent: boolean, signal?: AbortSignal) => mutate<Customer>(base + '/' + number + '/whatsapp-consent', 'PATCH', { consent }, signal)
export const deactivateCustomer = (number: number, signal?: AbortSignal) => mutate<void>(base + '/' + number + '/deactivate', 'PATCH', undefined, signal)
