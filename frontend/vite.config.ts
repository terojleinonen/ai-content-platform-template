import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiUrl = process.env.VITE_API_URL ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Forward API calls to the ASP.NET Core backend during development.
    proxy: { '/api': { target: apiUrl, changeOrigin: true } },
  },
  build: {
    // The backend serves the production build from its wwwroot folder.
    outDir: '../backend/src/AiContentPlatform.Api/wwwroot',
    emptyOutDir: true,
  },
})
