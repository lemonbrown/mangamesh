import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7030',
        changeOrigin: true,
        secure: false, // Allow self-signed certs
      },
      '/covers': {
        target: 'https://localhost:7030',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
