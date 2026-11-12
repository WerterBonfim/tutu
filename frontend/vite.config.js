import { svelte } from '@sveltejs/vite-plugin-svelte'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [svelte()],
  css: {
    modules: {
      localsConvention: 'camelCaseOnly',
    },
  },
  server: {
    proxy: {
      '/api': 'http://127.0.0.1:5080',
      '/metrics': 'http://127.0.0.1:5080',
      '/events': { target: 'http://127.0.0.1:5080', changeOrigin: true },
    },
  },
})
