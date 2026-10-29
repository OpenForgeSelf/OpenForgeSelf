import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

/**
 * 组件画廊专用 Vite 配置（与主应用 vite.config.ts、e2e 的 playwright.config.ts 解耦）。
 *
 * 关键点：
 * - 插件链与主应用保持一致：vue + tailwind + Element Plus 自动导入/组件解析。
 *   少了 ElementPlusResolver，模板里的 <ElXxx> 会解析不到（组件空白、无样式）。
 * - `dts: false`：不在 ct/ 下生成 auto-imports.d.ts / components.d.ts 这类生成物。
 * - 共享依赖别名到**宿主 node_modules 的同一份**：插件前端（Plugins/<X>/web）自己装了
 *   vue/element-plus，若按插件目录解析会拿到第二份 Vue 实例 → 响应式失效（经典双实例坑）。
 * - 独立端口 5190（CT_FRESH=1 时用 CT_PORT 换口），strictPort：被占用直接失败，不静默漂移。
 */
const webRoot = fileURLToPath(new URL('..', import.meta.url))
const pluginsRoot = fileURLToPath(new URL('../../Plugins', import.meta.url))
const hostModules = fileURLToPath(new URL('../node_modules', import.meta.url))

export default defineConfig({
  root: fileURLToPath(new URL('.', import.meta.url)),
  // 让 /logo/* 这类 public 资源在画廊里也真实可用（否则组件里的图片 404，基线会飘）
  publicDir: fileURLToPath(new URL('../public', import.meta.url)),
  plugins: [
    vue(),
    tailwindcss(),
    AutoImport({ resolvers: [ElementPlusResolver()], dts: false }),
    Components({ resolvers: [ElementPlusResolver()], dts: false }),
  ],
  resolve: {
    alias: [
      // 具体路径放前面；字符串别名只匹配「整体相等」或「前缀 + /」，'vue' 不会误吃 'vue-router'
      { find: '@', replacement: fileURLToPath(new URL('../src', import.meta.url)) },
      { find: 'vue', replacement: `${hostModules}/vue` },
      { find: 'element-plus', replacement: `${hostModules}/element-plus` },
      { find: '@element-plus/icons-vue', replacement: `${hostModules}/@element-plus/icons-vue` },
      { find: 'pinia', replacement: `${hostModules}/pinia` },
    ],
  },
  server: {
    host: '127.0.0.1',
    port: Number(process.env.CT_PORT) || 5190,
    strictPort: true,
    // 画廊 root 在 ct/，被预览的组件在 src/ 与 Plugins/ —— 显式放行，避免 /@fs/ 403
    fs: { allow: [webRoot, pluginsRoot] },
  },
})
