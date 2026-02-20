import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '^/api/import': {
        target: 'https://localhost:7124',
        changeOrigin: true,
        secure: false
      },
      '^/api/Series': {
        target: 'https://localhost:7124',
        changeOrigin: true,
        secure: false
      },
      '^/api/blob': {
        target: 'https://localhost:7124',
        changeOrigin: true,
        secure: false
      },
      '^/api/mangametadata': {
        target: 'https://localhost:7030',
        changeOrigin: true,
        secure: false
      },
      '^/api/auth': {
        target: 'https://localhost:7030',
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/api\/auth/, '/api')
      },
      '^/api/keys': {
        target: 'https://localhost:7124',
        changeOrigin: true,
        secure: false
      },
      '^/api/node': {
        target: 'https://localhost:7124',
        changeOrigin: true,
        secure: false
      },
      '^/api/subscriptions': {
        target: 'https://localhost:7124',
        changeOrigin: true,
        secure: false
      },
      '^/gateway': {
        target: 'http://localhost:5170',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/gateway/, '')
      }
    }
  }
})
