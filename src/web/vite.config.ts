import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// The SPA is built directly into the API project's wwwroot so a single
// multi-stage Docker image can serve it. During `npm run dev`, API calls are
// proxied to the ASP.NET Core backend on :8080.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../GluetunWeb.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
})
