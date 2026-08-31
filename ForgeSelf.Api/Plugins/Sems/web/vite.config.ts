import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * sems 插件界面构建配置（与 AIAgent 一致，遵循 spec 010 契约）。
 * 产物为标准 ESM（lib 模式），入口固定 dist/index.js；共享依赖 external，
 * 由宿主页面的 import map 解析到宿主同一份实例，避免 Vue 双实例。
 * 界面一律用原生 HTML + CSS，不用 <ElXxx> 组件（插件是独立预编译产物，宿主不会处理）。
 */
export default defineConfig({
  plugins: [vue()],
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