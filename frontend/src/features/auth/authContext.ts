import { createContext, useContext } from 'react'
import type { Employee } from '../employees/employeeApi'

export interface Session {
  status: 'loading' | 'authenticated' | 'unauthenticated'
  employee: Employee | null
  error: unknown
  refresh: () => Promise<void>
  signIn: (username: string, password: string) => Promise<void>
  signOut: () => Promise<void>
}
export const AuthContext = createContext<Session | null>(null)
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('AuthProvider is required')
  return context
}
