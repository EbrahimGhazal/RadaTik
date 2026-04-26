import path from 'node:path'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
// يُبنى المخرجات إلى مشروع ASP.NET ليُخدم تحت /app
const webRootApp = path.resolve(__dirname, '../RadTik/wwwroot/app')

export default defineConfig({
  base: '/app/',
  plugins: [react(), tailwindcss()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    globals: true,
  },
  build: {
    outDir: webRootApp,
    emptyOutDir: true,
  },
  server: {
    proxy: {
      // مصادقة SPA → ASP.NET (قبل أي /api عام يتجه لـ json-server)
      '/api/spa-auth': {
        target: process.env.VITE_ASPNET_URL ?? 'https://localhost:7098',
        changeOrigin: true,
        secure: false,
      },
      '/api/manager': {
        target: process.env.VITE_ASPNET_URL ?? 'https://localhost:7098',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: process.env.VITE_ASPNET_URL ?? 'https://localhost:7098',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
      '/api': {
        target: 'http://localhost:3001',
        changeOrigin: true,
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
    },
  },
})
