import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
<<<<<<< HEAD
  css: {
    preprocessorOptions: {
      scss: {
        // Silence Sass deprecation warnings from Carbon's internal @use patterns.
        // Remove this silencer once @carbon/styles ships a Sass 2.x-clean build.
        quietDeps: true,
      },
    },
  },
=======
>>>>>>> 6c8d2ece674506c5f8db44076b4aeabe57cf9f87
})
