import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * AIAgent 插件界面构建配置。
 *
 * 要点：
 * - 产物为标准 ESM（lib 模式，formats: ['es']），入口固定 dist/index.js。
 * - 共享依赖（vue / vue-router / pinia / element-plus）一律 external：
 *   产物内保留裸导入，由宿主页面的 import map 解析到宿主**同一份**实例，
 *   从根本上避免 Vue 双实例导致的响应式失效。
 * - 不使用 Element Plus 组件：宿主的 EP 组件由 unplugin-vue-components 在
 *   **编译期局部注册**，而本插件是独立构建的预编译产物，宿主打包器不会处理它，
 *   因此模板里写 <ElXxx> 在运行时会解析失败。试点界面一律用原生 HTML + CSS。
 *   （程序化 API 如 ElMessage 仍可通过 import map 使用。）
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
        // 固定为 style.css：lib 模式下 CSS 是独立产物、不会被 JS 引用，
        // 宿主加载器按「入口同目录的 style.css」约定注入 <link>，名字必须稳定可预期。
        assetFileNames: 'style[extname]',
      },
    },
  },
})
