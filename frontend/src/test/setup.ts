import '@testing-library/jest-dom/vitest'
import { afterEach, vi } from 'vitest'
import { cleanup } from '@testing-library/react'
afterEach(() => { cleanup(); vi.unstubAllEnvs(); vi.unstubAllGlobals() })
Object.defineProperty(window, 'scrollTo', { value: () => {}, writable: true })
