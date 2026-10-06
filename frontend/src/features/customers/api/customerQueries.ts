import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getCustomerByNumber } from './customerApi'
import type { Customer } from '../types/customer'
export const customerKeys = {
  all: ['customers'] as const,
  detail: (number: number) => ['customers', 'detail', number] as const,
  search: (query: string) => ['customers', 'search', query] as const,
}
export function useCustomer(number: number) {
  return useQuery({ queryKey: customerKeys.detail(number), queryFn: ({ signal }) => getCustomerByNumber(number, signal),
    enabled: Number.isInteger(number) && number > 0 })
}
export function useRefreshCustomer() {
  const client = useQueryClient()
  return async (customer?: Customer) => {
    await client.cancelQueries({ queryKey: customerKeys.all })
    if (customer) client.setQueryData(customerKeys.detail(customer.customerNumber), customer)
    await client.invalidateQueries({ queryKey: customerKeys.all })
  }
}
