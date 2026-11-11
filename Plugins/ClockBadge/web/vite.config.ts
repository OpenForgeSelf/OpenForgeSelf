import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * ClockBadge 插件界面构建配置（照抄 QuickLinks 模板：lib 模式 + 共享依赖 external）。
 *
 * 要点：
 * - 产物为标准 ESM（lib 模式），入口固定 dist/index.js。
 * - vue / vue-router / pinia / element-plus 一律 external：产物内保留裸导入，
 *   由宿主页面的 import map 解析到宿主同一份实例。
 * - 不使用 Element Plus 组件（宿主 EP 组件是编译期局部注册，独立预编译产物解析不到），
 *   界面一律原生 HTML + CSS。
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
        // 固定为 style.css：lib 模式下 CSS 是独立产物，宿主加载器按「入口同目录 style.css」注入 <link>。
        assetFileNames: 'style[extname]',
      },
    },
  },
})
