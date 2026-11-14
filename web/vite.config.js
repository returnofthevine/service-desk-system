import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      // Keep API and browser requests on one origin during development.
      '/api': 'http://127.0.0.1:5087',
    },
  },
})
