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
    server: { proxy: { '/api': { target, changeOrigin: true }, '/health': { target, changeOrigin: true } } },
  }
})
