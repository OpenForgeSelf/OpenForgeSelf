import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * ToolBridge 插件界面构建配置（形状同 Plugins/QuickLinks/web/vite.config.ts）。
 *
 * - 产物为标准 ESM（lib 模式），入口固定 dist/index.js，样式产物固定 style.css。
 * - vue / vue-router / pinia / element-plus 一律 external：产物保留裸导入，
 *   由宿主 import map 解析到宿主**同一份**实例（否则 Vue 双实例，inject/useRouter 返回 undefined）。
 * - 模板不使用 <ElXxx>：宿主 EP 组件由 unplugin 在宿主编译期注册，本插件是预编译产物，
 *   宿主打包器不会处理它 ⇒ 运行时解析失败。界面用原生 HTML + --el-* 变量（03-plan 决策 D4）。
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
