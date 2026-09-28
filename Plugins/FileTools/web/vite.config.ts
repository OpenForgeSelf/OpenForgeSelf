import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

/**
 * FileTools 插件自带界面构建配置（批次C 迁移：界面从宿主 src/ 迁入插件，此后改 UI 只需发插件）。
 *
 * 要点（契约见 specs/010-plugin-frontend-runtime）：
 * - 产物为标准 ESM（lib 模式，formats:['es']），入口固定 dist/index.js；CSS 固定 dist/style.css。
 * - vue / vue-router / pinia / element-plus 一律 external：产物内保留裸导入，由宿主 import map
 *   解析到宿主**同一份**实例（漏声明会内联第二份 Vue → inject/useRouter 返回 undefined 且只在点击时炸）。
 * - Tailwind 自带入口（styles/tailwind.css）：宿主只编译宿主的工具类，插件必须自备，
 *   否则迁移过来的面板工具类会失去样式。
 * - 界面内不使用 <ElXxx> 模板组件（宿主 EP 组件由 unplugin-vue-components 编译期注册，
 *   不会处理预编译产物）；程序化 API（ElMessageBox）经 import map 可用。
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
      fileName: () => 'index.js'
    },
    rollupOptions: {
      external: ['vue', 'vue-router', 'pinia', 'element-plus'],
      output: {
        entryFileNames: 'index.js',
        chunkFileNames: '[name].js',
        assetFileNames: 'style[extname]'
      }
    }
  }
})
