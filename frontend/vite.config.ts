import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            { name: 'mui', test: /node_modules[\\/]@mui[\\/]/, priority: 2 },
            { name: 'charts', test: /node_modules[\\/]recharts[\\/]/, priority: 2 },
            { name: 'vendor', test: /node_modules[\\/]/, minSize: 30000 },
          ],
        },
      },
    },
  },
})
