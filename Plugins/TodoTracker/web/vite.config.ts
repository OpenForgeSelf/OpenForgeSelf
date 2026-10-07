import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

/**
 * todo-tracker 插件界面构建配置（由 plugin-frontend-scaffold 从 AIAgent 模板整目录生成）。
 *
 * 要点：
 * - 产物为标准 ESM（lib 模式，formats: ['es']），入口固定 dist/index.js。
 * - 共享依赖（vue / vue-router / pinia / element-plus）一律 external：
 *   产物内保留裸导入，由宿主页面的 import map 解析到宿主**同一份**实例，
 *   从根本上避免 Vue 双实例导致的响应式失效。
 * - Tailwind：插件模板直接用工具类布局（tailwind.css 入口，@theme inline 映射 --el-*），
 *   由本插件自己的构建编译进 style.css（宿主只编译宿主的工具类，插件必须自备）。
 * - Element Plus 组件：插件经 import map 从宿主共享桥（public/shared/element-plus.js）
 *   取宿主同一份组件实例（宿主 exposeSharedDeps 已把所需组件挂到 __FORGE_SHARED__）。
 */
export default defineConfig({
  plugins: [vue(), tailwindcss()],
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
      external: ['vue', 'vue-router', 'pinia', 'element-plus', '@element-plus/icons-vue'],
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
