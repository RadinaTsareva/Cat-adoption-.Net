import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiProxyTarget = process.env.VITE_API_PROXY_TARGET || 'http://localhost:5154'

export default defineConfig({
  plugins: [react()],
  define: {
    __VITE_API_BACKEND__: JSON.stringify(process.env.VITE_API_URL ? `${process.env.VITE_API_URL}/api` : ''),
  },
  server: {
    host: '0.0.0.0',
    port: 3000,
    proxy: {
      '/api': {
        target: apiProxyTarget,
        changeOrigin: true,
      },
    },
  },
})

