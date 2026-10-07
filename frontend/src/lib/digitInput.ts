import type { ChangeEvent, ClipboardEvent } from 'react'

// Input filtering is UX only; callers retain their exact-length schema validation.
export function digitInputHandlers(limit: number, onValueChange: (value: string) => void) {
  const digits = (value: string) => value.replace(/[^0-9]/g, '')
  return {
    onChange(event: ChangeEvent<HTMLInputElement>) {
      event.target.value = digits(event.target.value).slice(0, limit)
      onValueChange(event.target.value)
    },
    onPaste(event: ClipboardEvent<HTMLInputElement>) {
      event.preventDefault()
      const input = event.currentTarget
      const pasted = digits(event.clipboardData.getData('text'))
      const start = input.selectionStart ?? input.value.length
      const end = input.selectionEnd ?? start
      const value = (input.value.slice(0, start) + pasted + input.value.slice(end)).slice(0, limit)
      input.value = value
      onValueChange(value)
      const caret = Math.min(start + pasted.length, value.length)
      input.setSelectionRange(caret, caret)
    },
  }
}
