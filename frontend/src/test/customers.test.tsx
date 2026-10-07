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
  for (const [name, value] of [['תעודת זהות', '123456789'], ['שם פרטי', 'Ahmad'], ['שם משפחה', 'Ali'],
    ['טלפון נייד', '0501234567'], ['עיר', 'Haifa']]) {
    await user.type(screen.getByLabelText(name + ' *'), value)
  }
  await user.type(screen.getByLabelText('יום'), '20')
  await user.type(screen.getByLabelText('חודש'), '4')
  await user.type(screen.getByLabelText('שנה'), '1995')
  await user.click(screen.getByRole('radio', { name: 'זכר' }))
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(api.getCustomerByNumber).mockResolvedValue(customer)
  vi.mocked(api.searchCustomers).mockResolvedValue([])
})
describe('customer workflows', () => {
  it('renders the Hebrew customer heading in the RTL application shell', () => {
    setup()
    expect(screen.getByRole('heading', { name: 'לקוחות' })).toBeVisible()
    expect(screen.getByRole('main').closest('[dir]')).toHaveAttribute('dir', 'rtl')
    expect(screen.getByRole('navigation', { name: 'ניווט ראשי' })).toBeVisible()
  })
  it('keeps numeric and contact inputs LTR inside the RTL form', () => {
    setup('/customers/new')
    for (const label of ['תעודת זהות *', 'טלפון נייד *', 'טלפון בבית', 'דוא״ל', 'יום', 'חודש', 'שנה']) {
      expect(screen.getByLabelText(label)).toHaveAttribute('dir', 'ltr')
    }
    expect(screen.getByLabelText('שם פרטי *')).not.toHaveAttribute('dir', 'ltr')
  })
  it('does not request blank or whitespace searches', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: 'חיפוש' }))
    await user.type(screen.getByLabelText('חיפוש לקוחות'), '   ')
    await user.click(screen.getByRole('button', { name: 'חיפוש' }))
    expect(api.searchCustomers).not.toHaveBeenCalled()
  })
  it('renders all mocked matches including shared-phone and inactive rows', async () => {
    vi.mocked(api.searchCustomers).mockResolvedValue([customer, { ...customer, id: 'customer-2', customerNumber: 43, firstName: 'Sara', isActive: false }])
    const { user } = setup()
    await user.type(screen.getByLabelText('חיפוש לקוחות'), '0501234567')
    await user.click(screen.getByRole('button', { name: 'חיפוש' }))
    expect(await screen.findByText('Ahmad Ali')).toBeVisible()
    expect(screen.getByText('Sara Ali')).toBeVisible()
    expect(screen.getByText('לא פעיל')).toBeVisible()
    expect(api.searchCustomers).toHaveBeenCalledWith('0501234567', expect.any(AbortSignal))
  })
  it('prevents invalid create and labels required fields', async () => {
    const { user } = setup('/customers/new')
    await user.click(screen.getByRole('button', { name: 'יצירת לקוח' }))
    expect(await screen.findAllByText('שדה זה הוא שדה חובה.')).toHaveLength(3)
    expect(screen.getByText('תעודת זהות חייבת להכיל בדיוק 9 ספרות.')).toBeVisible()
    expect(screen.getByText('טלפון נייד חייב להכיל בדיוק 10 ספרות.')).toBeVisible()
    expect(screen.getByText('יש לבחור מגדר.')).toBeVisible()
    expect(screen.getByText('יש להזין תאריך לידה תקין.')).toBeVisible()
    expect(api.createCustomer).not.toHaveBeenCalled()
  })
  it.each([['זכר', 'Male'], ['נקבה', 'Female']])('submits canonical gender %s and date then redirects to the generated number', async (label, gender) => {
    vi.mocked(api.createCustomer).mockResolvedValue({ ...customer, customerNumber: 1057 })
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, customerNumber: 1057 })
    const { user, router } = setup('/customers/new')
    await fillForm(user)
    await user.click(screen.getByRole('radio', { name: label }))
    await user.click(screen.getByRole('button', { name: 'יצירת לקוח' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/customers/1057'))
    expect(api.createCustomer).toHaveBeenCalledWith(expect.objectContaining({ nationalId: '123456789', gender, dateOfBirth: '1995-04-20', homePhone: null, email: null }))
  })
  it('shows duplicate feedback and preserves input', async () => {
    vi.mocked(api.createCustomer).mockRejectedValue(new ApiError(409, 'לקוח עם תעודת זהות זו כבר קיים במערכת.'))
    const { user } = setup('/customers/new')
    await fillForm(user)
    await user.click(screen.getByRole('button', { name: 'יצירת לקוח' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('כבר קיים במערכת')
    expect(screen.getByLabelText('שם פרטי *')).toHaveValue('Ahmad')
  })
  it('shows inactive details and disables deactivation', async () => {
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, isActive: false })
    setup('/customers/42')
    expect(await screen.findByText('לא פעיל')).toBeVisible()
    expect(screen.getByRole('button', { name: 'השבתת לקוח' })).toBeDisabled()
    expect(screen.getByRole('heading', { name: 'Ahmad Ali' })).toBeVisible()
  })
  it('requires confirmation and updates deactivated status', async () => {
    vi.mocked(api.deactivateCustomer).mockImplementation(async () => {
      vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, isActive: false })
    })
    const { user } = setup('/customers/42')
    await user.click(await screen.findByRole('button', { name: 'השבתת לקוח' }))
    const dialog = screen.getByRole('dialog')
    expect(api.deactivateCustomer).not.toHaveBeenCalled()
    expect(dialog).toHaveTextContent('הרשומה תישאר במערכת')
    await user.click(within(dialog).getByRole('button', { name: 'אישור השבתה' }))
    await waitFor(() => expect(api.deactivateCustomer).toHaveBeenCalledTimes(1))
    expect(await screen.findByText('לא פעיל')).toBeVisible()
  })
  it('cancels deactivation without mutation', async () => {
    const { user } = setup('/customers/42')
    await user.click(await screen.findByRole('button', { name: 'השבתת לקוח' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'ביטול' }))
    expect(api.deactivateCustomer).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
  it('retains original consent and shows feedback when save fails', async () => {
    vi.mocked(api.setWhatsAppConsent).mockRejectedValue(new ApiError(500, 'לא ניתן להשלים את הפעולה. נסה שוב.'))
    const { user } = setup('/customers/42')
    const checkbox = await screen.findByRole('checkbox', { name: 'הסכמה להודעות WhatsApp' })
    await user.click(checkbox)
    expect(await screen.findByRole('alert')).toHaveTextContent('נסה שוב')
    expect(checkbox).not.toBeChecked()
  })
  it('updates consent on success', async () => {
    const changed = { ...customer, whatsAppConsent: true }
    vi.mocked(api.setWhatsAppConsent).mockImplementation(async () => {
      vi.mocked(api.getCustomerByNumber).mockResolvedValue(changed)
      return changed
    })
    const { user } = setup('/customers/42')
    await user.click(await screen.findByRole('checkbox', { name: 'הסכמה להודעות WhatsApp' }))
    await waitFor(() => expect(screen.getByRole('checkbox')).toBeChecked())
  })
  it('edit sends only editable fields and refreshes details', async () => {
    vi.mocked(api.updateCustomer).mockImplementation(async (_number, values) => {
      const changed = { ...customer, ...values }
      vi.mocked(api.getCustomerByNumber).mockResolvedValue(changed)
      return changed
    })
    const { user, router } = setup('/customers/42/edit')
    const name = await screen.findByLabelText('שם פרטי *')
    expect(screen.getByRole('radio', { name: 'זכר' })).toBeChecked()
    expect(screen.getByLabelText('יום')).toHaveValue('20')
    expect(screen.getByLabelText('חודש')).toHaveValue('04')
    expect(screen.getByLabelText('שנה')).toHaveValue('1995')
    expect(screen.queryByLabelText('תעודת זהות *')).not.toBeInTheDocument()
    await user.clear(name)
    await user.type(name, 'Sara')
    await user.click(screen.getByRole('button', { name: 'שמירת שינויים' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/customers/42'))
    expect(await screen.findByRole('heading', { name: 'Sara Ali' })).toBeVisible()
    const payload = vi.mocked(api.updateCustomer).mock.calls[0][1]
    expect(payload).not.toHaveProperty('nationalId')
    expect(payload).not.toHaveProperty('whatsAppConsent')
    expect(payload).not.toHaveProperty('isActive')
    expect(payload.dateOfBirth).toBe('1995-04-20')
  })
  it('uses mutually exclusive Hebrew radios and locale-independent DOB fields', async () => {
    const { user } = setup('/customers/new')
    const male = screen.getByRole('radio', { name: 'זכר' })
    const female = screen.getByRole('radio', { name: 'נקבה' })
    expect(male).not.toBeChecked()
    expect(female).not.toBeChecked()
    await user.click(male)
    await user.click(female)
    expect(female).toBeChecked()
    expect(male).not.toBeChecked()
    for (const label of ['יום', 'חודש', 'שנה']) {
      expect(screen.getByPlaceholderText(label)).toHaveAttribute('type', 'text')
      expect(screen.getByLabelText(label)).toHaveAttribute('inputmode', 'numeric')
    }
    expect(document.querySelector('input[type="date"]')).toBeNull()
  })
  it('shows immediate Hebrew feedback and blocks impossible dates', async () => {
    const { user } = setup('/customers/new')
    await fillForm(user)
    await user.clear(screen.getByLabelText('חודש'))
    await user.type(screen.getByLabelText('חודש'), '2')
    await user.clear(screen.getByLabelText('יום'))
    await user.type(screen.getByLabelText('יום'), '31')
    expect(await screen.findByText('יש להזין תאריך לידה תקין.')).toBeVisible()
    await user.type(screen.getByLabelText('טלפון בבית'), 'abc')
    expect(await screen.findByText('טלפון בבית חייב להכיל בדיוק 9 ספרות.')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'יצירת לקוח' }))
    expect(api.createCustomer).not.toHaveBeenCalled()
  })
  it('preselects persisted Female when editing', async () => {
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, gender: 'Female' })
    setup('/customers/42/edit')
    expect(await screen.findByRole('radio', { name: 'נקבה' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'זכר' })).not.toBeChecked()
  })
  it('explains a legacy invalid immutable ID instead of silently blocking edit', async () => {
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, nationalId: 'legacy-invalid' })
    const { user } = setup('/customers/42/edit')
    await user.click(await screen.findByRole('button', { name: 'שמירת שינויים' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('לא ניתן לתקן אותה במסך זה')
    expect(api.updateCustomer).not.toHaveBeenCalled()
  })
  it.each([['Male', 'זכר'], ['Female', 'נקבה']])('shows canonical %s in Hebrew on details', async (gender, label) => {
    vi.mocked(api.getCustomerByNumber).mockResolvedValue({ ...customer, gender })
    setup('/customers/42')
    expect(await screen.findByText(label)).toBeVisible()
  })
  it('renders a friendly missing-customer state', async () => {
    vi.mocked(api.getCustomerByNumber).mockRejectedValue(new ApiError(404, 'הלקוח לא נמצא.'))
    setup('/customers/999')
    expect(await screen.findByRole('alert')).toHaveTextContent('הלקוח לא נמצא')
  })
})
