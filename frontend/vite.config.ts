import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'OPTICORE_')
  const target = env.OPTICORE_BACKEND_URL || 'http://localhost:5063'
  return {
    plugins: [react(), tailwindcss()],
    resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
    // Preserve the browser Host so the backend can compare it to Origin for CSRF protection.
    server: { proxy: { '/api': { target, changeOrigin: false }, '/health': { target, changeOrigin: false } } },
  }
})
