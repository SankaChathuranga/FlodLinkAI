import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
  css: {
    preprocessorOptions: {
      scss: {
        // Silence Sass deprecation warnings from Carbon's internal @use patterns.
        // Remove this silencer once @carbon/styles ships a Sass 2.x-clean build.
        quietDeps: true,
      },
    },
  },
})
