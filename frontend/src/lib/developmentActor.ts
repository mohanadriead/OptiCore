const storageKey = 'opticore.development-actor'
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// NOT authentication. Remove this temporary browser identity when real authentication exists.
export function developmentActor(): string {
  if (!import.meta.env.DEV) throw new Error('Customer changes are unavailable until authentication is configured.')
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored && uuid.test(stored) && stored !== '00000000-0000-0000-0000-000000000000') return stored
    const actor = crypto.randomUUID()
    localStorage.setItem(storageKey, actor)
    return actor
  } catch {
    throw new Error('Enable browser storage to save customer changes in development.')
  }
}
