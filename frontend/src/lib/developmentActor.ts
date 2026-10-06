const storageKey = 'opticore.development-actor'
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// NOT authentication. Remove this temporary browser identity when real authentication exists.
export function developmentActor(): string {
  if (!import.meta.env.DEV) throw new Error('לא ניתן לשמור שינויים בלקוחות עד להגדרת הזדהות.')
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored && uuid.test(stored) && stored !== '00000000-0000-0000-0000-000000000000') return stored
    const actor = crypto.randomUUID()
    localStorage.setItem(storageKey, actor)
    return actor
  } catch {
    throw new Error('יש לאפשר אחסון בדפדפן כדי לשמור שינויים בלקוחות בסביבת הפיתוח.')
  }
}
