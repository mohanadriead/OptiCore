import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from '@/app/router'
import * as api from '@/features/customers/api/customerApi'
import { ApiError } from '@/lib/apiClient'
import type { Customer } from '@/features/customers/types/customer'

vi.mock('@/features/customers/api/customerApi')
const customer: Customer = {
  id: 'customer-1', customerNumber: 42, nationalId: '123456789', firstName: 'Ahmad', lastName: 'Ali',
  dateOfBirth: '1995-04-20', mobilePhone: '0501234567', homePhone: null, email: null,
  city: 'Haifa', street: null, gender: 'Male', notes: null, whatsAppConsent: false, isActive: true,
  createdAtUtc: '2026-01-01T10:00:00Z', updatedAtUtc: null,
}
function setup(path = '/customers') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(<QueryClientProvider client={client}><RouterProvider router={router} /></QueryClientProvider>)
  return { user: userEvent.setup(), router }
}
async function fillForm(user: ReturnType<typeof userEvent.setup>) {
  for (const [name, value] of [['National ID', '123456789'], ['First Name', 'Ahmad'], ['Last Name', 'Ali'],
    ['Date of Birth', '1995-04-20'], ['Mobile Phone', '0501234567'], ['City', 'Haifa'], ['Gender', 'Unspecified']]) {
    await user.type(screen.getByLabelText(name + ' *'), value)
  }
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(api.getCustomerByNumber).mockResolvedValue(customer)
  vi.mocked(api.searchCustomers).mockResolvedValue([])
})
describe('customer workflows', () => {
  it('does not request blank or whitespace searches', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: 'Search' }))
    await user.type(screen.getByLabelText('Search customers'), '   ')
    await user.click(screen.getByRole('button', { name: 'Search' }))
    expect(api.searchCustomers).not.toHaveBeenCalled()
  })
  it('renders all mocked matches including shared-phone and inactive rows', async () => {
    vi.mocked(api.searchCustomers).mockResolvedValue([customer, { ...customer, id: 'customer-2', customerNumber: 43, firstName: 'Sara', isActive: false }])
    const { user } = setup()
    await user.type(screen.getByLabelText('Search customers'), '0501234567')
    await user.click(screen.getByRole('button', { name: 'Search' }))
    expect(await screen.findByText('Ahmad Ali')).toBeVisible()
    expect(screen.getByText('Sara Ali')).toBeVisible()
    expect(screen.getByText('Inactive')).toBeVisible()
    expect(api.searchCustomers).toHaveBeenCalledWith('0501234567', expect.any(AbortSignal))
  })
  it('prevents invalid create and labels required fields', async () => {
    const { user } = setup('/customers/new')
    await user.click(screen.getByRole('button', { name: 'Create Customer' }))
    expect(await screen.findAllByText('This field is required.')).toHaveLength(7)
    expect(api.createCustomer).not.toHaveBeenCalled()
  })
  it('redirects successful create to the generated customer number', async () => {
    vi.mocked(api.createCustomer).mockResolvedValue({ ...customer, customerNumber: 1057 })
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, customerNumber: 1057 })
    const { user, router } = setup('/customers/new')
    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Create Customer' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/customers/1057'))
    expect(api.createCustomer).toHaveBeenCalledWith(expect.objectContaining({ nationalId: '123456789', gender: 'Unspecified', email: null }))
  })
  it('shows duplicate feedback and preserves input', async () => {
    vi.mocked(api.createCustomer).mockRejectedValue(new ApiError(409, 'A customer with this National ID already exists.'))
    const { user } = setup('/customers/new')
    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'Create Customer' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('already exists')
    expect(screen.getByLabelText('First Name *')).toHaveValue('Ahmad')
  })
  it('shows inactive details and disables deactivation', async () => {
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, isActive: false })
    setup('/customers/42')
    expect(await screen.findByText('Inactive')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Deactivate Customer' })).toBeDisabled()
    expect(screen.getByRole('heading', { name: 'Ahmad Ali' })).toBeVisible()
  })
  it('requires confirmation and updates deactivated status', async () => {
    vi.mocked(api.deactivateCustomer).mockImplementation(async () => {
      vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, isActive: false })
    })
    const { user } = setup('/customers/42')
    await user.click(await screen.findByRole('button', { name: 'Deactivate Customer' }))
    const dialog = screen.getByRole('dialog')
    expect(api.deactivateCustomer).not.toHaveBeenCalled()
    expect(dialog).toHaveTextContent('record will remain')
    await user.click(within(dialog).getByRole('button', { name: 'Confirm Deactivation' }))
    await waitFor(() => expect(api.deactivateCustomer).toHaveBeenCalledTimes(1))
    expect(await screen.findByText('Inactive')).toBeVisible()
  })
  it('cancels deactivation without mutation', async () => {
    const { user } = setup('/customers/42')
    await user.click(await screen.findByRole('button', { name: 'Deactivate Customer' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }))
    expect(api.deactivateCustomer).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
  it('retains original consent and shows feedback when save fails', async () => {
    vi.mocked(api.setWhatsAppConsent).mockRejectedValue(new ApiError(500, 'Unable to complete the request. Please try again.'))
    const { user } = setup('/customers/42')
    const checkbox = await screen.findByRole('checkbox', { name: 'WhatsApp consent' })
    await user.click(checkbox)
    expect(await screen.findByRole('alert')).toHaveTextContent('try again')
    expect(checkbox).not.toBeChecked()
  })
  it('updates consent on success', async () => {
    const changed = { ...customer, whatsAppConsent: true }
    vi.mocked(api.setWhatsAppConsent).mockImplementation(async () => {
      vi.mocked(api.getCustomerByNumber).mockResolvedValue(changed)
      return changed
    })
    const { user } = setup('/customers/42')
    await user.click(await screen.findByRole('checkbox', { name: 'WhatsApp consent' }))
    await waitFor(() => expect(screen.getByRole('checkbox')).toBeChecked())
  })
  it('edit sends only editable fields and refreshes details', async () => {
    vi.mocked(api.updateCustomer).mockImplementation(async (_number, values) => {
      const changed = { ...customer, ...values }
      vi.mocked(api.getCustomerByNumber).mockResolvedValue(changed)
      return changed
    })
    const { user, router } = setup('/customers/42/edit')
    const name = await screen.findByLabelText('First Name *')
    expect(screen.queryByLabelText('National ID *')).not.toBeInTheDocument()
    await user.clear(name)
    await user.type(name, 'Sara')
    await user.click(screen.getByRole('button', { name: 'Save Changes' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/customers/42'))
    expect(await screen.findByRole('heading', { name: 'Sara Ali' })).toBeVisible()
    const payload = vi.mocked(api.updateCustomer).mock.calls[0][1]
    expect(payload).not.toHaveProperty('nationalId')
    expect(payload).not.toHaveProperty('whatsAppConsent')
    expect(payload).not.toHaveProperty('isActive')
  })
  it('renders a friendly missing-customer state', async () => {
    vi.mocked(api.getCustomerByNumber).mockRejectedValue(new ApiError(404, 'Customer not found.'))
    setup('/customers/999')
    expect(await screen.findByRole('alert')).toHaveTextContent('Customer not found')
  })
})
