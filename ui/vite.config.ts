import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Vite proxies /api and /hubs to the local ASP.NET Core API (127.0.0.1:5000).
// /hubs is upgraded to a WebSocket so SignalR negotiate + transport works.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://127.0.0.1:5000', changeOrigin: true },
      '/hubs': { target: 'http://127.0.0.1:5000', changeOrigin: true, ws: true },
      '/openapi': { target: 'http://127.0.0.1:5000', changeOrigin: true },
    },
  },
})
