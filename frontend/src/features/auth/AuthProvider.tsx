import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/apiClient'
import { discardSessionRequests, onSessionInvalidated } from '@/lib/sessionEvents'
import { AuthContext, type Session } from './authContext'
import * as api from './authApi'
import type { Employee } from '../employees/employeeApi'

export function AuthProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient()
  const [state, setState] = useState<Pick<Session, 'status' | 'employee' | 'error'>>({ status: 'loading', employee: null, error: null })
  const generation = useRef(0)
  const current = useRef<Employee | null>(null)
  const clear = useCallback(() => {
    generation.current++
    discardSessionRequests()
    current.current = null
    client.clear()
    setState({ status: 'unauthenticated', employee: null, error: null })
  }, [client])
  const accept = useCallback((employee: Employee) => {
    if (current.current?.id !== employee.id) { discardSessionRequests(); client.clear() }
    else if (current.current.isManager && !employee.isManager) client.removeQueries({ queryKey: ['employees'] })
    current.current = employee
    setState({ status: 'authenticated', employee, error: null })
  }, [client])
  const refresh = useCallback(async (signal?: AbortSignal) => {
    const version = ++generation.current
    try {
      const employee = await api.getCurrentEmployee(signal)
      if (version === generation.current && !signal?.aborted) accept(employee)
    } catch (error) {
      if (version !== generation.current || signal?.aborted) return
      if (error instanceof ApiError && error.status === 401) clear()
      else setState({ status: 'loading', employee: null, error })
    }
  }, [accept, clear])
  const supersede = useCallback(() => { generation.current++ }, [])
  useEffect(() => {
    const controller = new AbortController()
    const unsubscribe = onSessionInvalidated(clear)
    void refresh(controller.signal)
    const focus = () => { if (current.current) void refresh() }
    window.addEventListener('focus', focus)
    return () => { controller.abort(); supersede(); unsubscribe(); window.removeEventListener('focus', focus) }
  }, [clear, refresh, supersede])
  const signIn = async (username: string, password: string) => {
    const version = ++generation.current
    const employee = await api.login(username, password)
    if (version === generation.current) accept(employee)
  }
  const signOut = async () => {
    generation.current++
    discardSessionRequests()
    await api.logout()
    clear()
  }
  return <AuthContext.Provider value={{ ...state, refresh, signIn, signOut }}>{children}</AuthContext.Provider>
}
