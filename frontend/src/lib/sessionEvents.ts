const listeners = new Set<() => void>()
let revision = 0
export const sessionRevision = () => revision
export function discardSessionRequests() { revision++ }
export function onSessionInvalidated(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}
export function invalidateSession() { listeners.forEach(listener => listener()) }
