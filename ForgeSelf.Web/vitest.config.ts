import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import { fileURLToPath, URL } from 'node:url'

/**
 * 宿主 node_modules 的绝对路径。
 * 插件前端的**组件测试**（如 AIAgent 的 SessionPanel.test.ts）位于宿主 root 之外，
 * 而插件自身不安装这些包（插件构建把它们全部 external，运行期由宿主 import map 提供），
 * 且宿主与插件是兄弟目录、Node 向上查找到不了宿主 node_modules —— 故测试期显式指向宿主这一份。
 */
const hostModules = fileURLToPath(new URL('./node_modules', import.meta.url))

export default defineConfig({
  plugins: [
    vue(),
    AutoImport({
      resolvers: [ElementPlusResolver({ importStyle: false })],
    }),
    Components({
      resolvers: [ElementPlusResolver({ importStyle: false })],
    }),
  ],
  test: {
    globals: true,
    environment: 'jsdom',
    // 插件前端的纯逻辑单测也在此入口跑（`pnpm run test` 一条命令覆盖宿主 + 插件，
    // 避免为每个插件单独装一份 vitest）。插件只写 `*.test.ts`（`*.spec.ts` 留给 Playwright e2e）。
    include: [
      'src/**/*.{test,spec}.{ts,tsx}',
      '../ForgeSelf.Api/Plugins/*/web/src/**/*.test.{ts,tsx}',
    ],
    exclude: [
      'node_modules',
      'dist',
      'e2e/**',
      '**/*.spec.ts',
      '**/*.test-d.ts',
    ],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: [
        'node_modules/',
        'src/**/*.d.ts',
        'src/main.ts',
        '**/*.config.ts',
        '**/*.config.js',
      ],
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // 仅测试期生效的三个裸模块名 → 宿主同一份实例（避免插件再装一份、避免 Vue 双实例）。
      // 注意：Vite 的字符串 alias 按「整体相等或后接 /」匹配，'vue' 不会误吃 'vue-router'。
      vue: `${hostModules}/vue`,
      '@vue/test-utils': `${hostModules}/@vue/test-utils`,
      '@element-plus/icons-vue': `${hostModules}/@element-plus/icons-vue`,
    }
  },
  server: {
    fs: {
      // 插件测试位于宿主 root 之外（../ForgeSelf.Api/Plugins/*/web），
      // 必须显式放行，否则 Vite 拒绝加载并报 "Does the file exist?"（实为 fs allow 限制）。
      allow: [
        fileURLToPath(new URL('.', import.meta.url)),
        fileURLToPath(new URL('../ForgeSelf.Api/Plugins', import.meta.url)),
      ],
    },
  },
})