import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * IM 多渠道网关插件界面构建配置（契约与 AIAgent / DesignSystem 一致，specs/010-plugin-frontend-runtime）。
 *
 * 要点：
 * - 产物为标准 ESM（lib 模式，formats: ['es']），入口固定 dist/index.js。
 * - 共享依赖（vue / vue-router / pinia / element-plus）一律 external：
 *   由宿主页面 import map 解析到宿主同一份实例，避免 Vue 双实例。
 * - 不使用 Element Plus 组件：插件是独立预编译产物，宿主 unplugin-vue-components 不会处理它，
 *   模板里写 <ElXxx> 在运行时会解析失败。本界面一律用原生 HTML + CSS + --el-* token 变量。
 * - 配置页只读/写后端 /api/im-gateway/config、读 /api/im-gateway/status，
 *   认证从 localStorage['forge_api_token'] 取 token（与宿主 services/request.ts 一致）。
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
        // 宿主加载器按「入口同目录的 style.css」约定注入 <link>。
        assetFileNames: 'style[extname]',
      },
    },
  },
})
