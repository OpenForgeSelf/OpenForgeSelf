import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 版本串单一来源：视图内版本徽标从 plugin.json 构建期注入，禁止在视图里硬编码
// （2026-11-15 实测：清单 bump 后视图字面量滞后，徽标报旧版；PC-034 批 live 走查发现）
const pluginManifest = JSON.parse(
  readFileSync(fileURLToPath(new URL('../plugin.json', import.meta.url)), 'utf-8'),
) as { Version: string }

export default defineConfig({
  plugins: [vue()],
  define: {
    __PLUGIN_VERSION__: JSON.stringify(pluginManifest.Version),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    cssCodeSplit: false,
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'index.js',
    },
    rollupOptions: {
      external: ['vue', 'vue-router', 'pinia', 'element-plus'],
      output: {
        entryFileNames: 'index.js',
        chunkFileNames: '[name].js',
        assetFileNames: 'style[extname]',
      },
    },
  },
})
