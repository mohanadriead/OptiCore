import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router'
import { Toaster } from 'sonner'
import { queryClient } from './queryClient'
import { router } from './router'
import { appDirection } from './direction'
export function App() {
  return <QueryClientProvider client={queryClient}><RouterProvider router={router} /><Toaster richColors dir={appDirection} containerAriaLabel="התראות" toastOptions={{ closeButtonAriaLabel: 'סגירת התראה' }} /></QueryClientProvider>
}
