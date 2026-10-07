import { useState } from 'react'
import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DateOfBirthInput } from '@/features/customers/components/DateOfBirthInput'
import { detailsSchema } from '@/features/customers/schemas/customerSchemas'

function ControlledDate({ initial = '', changed = () => {} }: { initial?: string; changed?: (value: string) => void }) {
  const [value, setValue] = useState(initial)
  return <div dir="rtl"><DateOfBirthInput id="dob" value={value} onBlur={() => {}} onChange={next => { setValue(next); changed(next) }} />
    <output aria-label="ערך תאריך">{value}</output>
    {value && !detailsSchema.shape.dateOfBirth.safeParse(value).success && <p role="alert">יש להזין תאריך לידה תקין.</p>}
  </div>
}

it.each([
  { order: [['יום', '07'], ['חודש', '03'], ['שנה', '1995']] },
  { order: [['שנה', '1995'], ['חודש', '03'], ['יום', '07']] },
])('preserves each partial selection in order %# and emits only a complete canonical date', async ({ order }) => {
  const changed = vi.fn()
  render(<ControlledDate changed={changed} />)
  const user = userEvent.setup()
  for (let index = 0; index < order.length; index++) {
    const [label, value] = order[index]
    await user.selectOptions(screen.getByRole('combobox', { name: label }), value)
    for (const [selectedLabel, selectedValue] of order.slice(0, index + 1)) {
      expect(screen.getByLabelText(selectedLabel)).toHaveValue(selectedValue)
    }
    expect(screen.getByLabelText('ערך תאריך').textContent).toBe(index === 2 ? '1995-03-07' : '')
  }
  expect(changed.mock.calls.map(([value]) => value)).toEqual(['', '', '1995-03-07'])
  expect(document.querySelector('input[type="date"]')).toBeNull()
})
it('initializes edit dropdowns and synchronizes a different persisted date', () => {
  const props = { id: 'dob', onChange: vi.fn(), onBlur: vi.fn() }
  const { rerender } = render(<DateOfBirthInput {...props} value="1995-04-20" />)
  for (const [label, value] of [['יום', '20'], ['חודש', '04'], ['שנה', '1995']]) expect(screen.getByLabelText(label)).toHaveValue(value)
  rerender(<DateOfBirthInput {...props} value="2000-02-29" />)
  for (const [label, value] of [['יום', '29'], ['חודש', '02'], ['שנה', '2000']]) expect(screen.getByLabelText(label)).toHaveValue(value)
  expect(props.onChange).not.toHaveBeenCalled()
  rerender(<DateOfBirthInput {...props} value="" />)
  for (const label of ['יום', 'חודש', 'שנה']) expect(screen.getByLabelText(label)).toHaveValue('')
})
it('retains the other selections when clearing and reselecting part of a complete date', async () => {
  render(<ControlledDate initial="1995-04-20" />)
  const user = userEvent.setup()
  await user.selectOptions(screen.getByLabelText('יום'), '')
  expect(screen.getByLabelText('ערך תאריך')).toBeEmptyDOMElement()
  expect(screen.getByLabelText('חודש')).toHaveValue('04')
  expect(screen.getByLabelText('שנה')).toHaveValue('1995')
  await user.selectOptions(screen.getByLabelText('יום'), '21')
  expect(screen.getByLabelText('ערך תאריך')).toHaveTextContent('1995-04-21')
})
it('leaves an impossible full date to the existing schema validation', async () => {
  render(<ControlledDate />)
  const user = userEvent.setup()
  await user.selectOptions(screen.getByLabelText('יום'), '31')
  await user.selectOptions(screen.getByLabelText('חודש'), '02')
  await user.selectOptions(screen.getByLabelText('שנה'), '2000')
  expect(screen.getByLabelText('ערך תאריך')).toHaveTextContent('2000-02-31')
  expect(screen.getByRole('alert')).toHaveTextContent('יש להזין תאריך לידה תקין.')
})
