import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * 成本观测插件界面构建配置（契约见 specs/010-plugin-frontend-runtime，与 DesignSystem / FileTools / McpCenter 一致）。
 *
 * 要点：
 * - 产物为标准 ESM（**lib 模式**，formats: ['es']），入口固定 dist/index.js —— 与 plugin.json 的
 *   frontend.entry 一致；宿主加载器按这个名字远程加载。
 * - **必须用 lib 模式**：非 lib 模式下 Vite 以 index.html 为入口，而插件没有 HTML 入口
 *   （界面由宿主页面挂载），会报「Could not resolve entry module "index.html"」。
 * - vue 一律 external：产物内保留裸导入，由宿主 import map 解析到宿主**同一份**实例，
 *   否则会内联第二份 Vue —— 响应式失效且只在点击时才炸。
 * - cssCodeSplit: false + assetFileNames: 'style[extname]'：lib 模式下 CSS 是独立产物、不会被 JS 引用，
 *   宿主按「入口同目录的 style.css」约定注入 <link>，所以名字必须稳定可预期。
 * - 界面内不使用 <ElXxx> 模板组件（宿主的 Element Plus 组件由 unplugin-vue-components 在编译期注册，
 *   不会处理预编译产物）；一律用原生 HTML + CSS + var(--el-*) 变量。
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
      external: ['vue'],
      output: {
        entryFileNames: 'index.js',
        chunkFileNames: '[name].js',
        assetFileNames: 'style[extname]',
      },
    },
  },
  server: {
    port: 7402,
    proxy: {
      // 本地开发时把插件端点代理到宿主
      '/api': { target: 'http://localhost:7300', changeOrigin: true },
    },
  },
})
