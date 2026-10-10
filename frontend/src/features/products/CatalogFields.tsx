import type { ReactNode } from 'react'
export const selectClass = 'h-10 w-full rounded-md border bg-white px-3 text-sm'
export function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="grid gap-2 text-sm font-medium"><span>{label}</span>{children}</label>
}
export function Section({ title, children }: { title: string; children: ReactNode }) {
  return <fieldset className="rounded-lg border bg-white p-5"><legend className="px-2 font-semibold">{title}</legend><div className="grid gap-4 sm:grid-cols-2">{children}</div></fieldset>
}
