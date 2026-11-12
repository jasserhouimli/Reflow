import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id: string) {
          if (id.includes('@xyflow')) return 'flow-vendor';
          if (id.includes('react-dom') || id.includes('react/')) return 'react-vendor';
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5001',
      '/auth': 'http://localhost:5001',
      '/hubs': {
        target: 'http://localhost:5001',
        ws: true,
      },
    },
  },
})
