import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';
import AutoImport from 'unplugin-auto-import/vite';
import Components from 'unplugin-vue-components/vite';
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers';

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    AutoImport({
      resolvers: [ElementPlusResolver()],
    }),
    Components({
      resolvers: [ElementPlusResolver()],
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  server: {
    port: 7002,
    host: "0.0.0.0",
    proxy: {
      '/api': {
        target: 'http://localhost:7102',
        changeOrigin: true
      },
      '/openapi': {
        target: 'http://localhost:7102',
        changeOrigin: true
      },
      '/scalar': {
        target: 'http://localhost:7102',
        changeOrigin: true
      }
    }
  }
});