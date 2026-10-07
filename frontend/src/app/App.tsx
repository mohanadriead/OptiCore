import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router'
import { Toaster } from 'sonner'
import { queryClient } from './queryClient'
import { router } from './router'
import { appDirection } from './direction'
import { AuthProvider } from '@/features/auth/AuthProvider'
export function App() {
  return <QueryClientProvider client={queryClient}><AuthProvider><RouterProvider router={router} /></AuthProvider><Toaster richColors dir={appDirection} containerAriaLabel="התראות" toastOptions={{ closeButtonAriaLabel: 'סגירת התראה' }} /></QueryClientProvider>
}
