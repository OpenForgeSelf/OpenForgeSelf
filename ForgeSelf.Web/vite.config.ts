import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';
import AutoImport from 'unplugin-auto-import/vite';
import Components from 'unplugin-vue-components/vite';
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers';

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    AutoImport({
      resolvers: [ElementPlusResolver()],
    }),
    Components({
      resolvers: [ElementPlusResolver()],
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  build: {
    outDir: '../ForgeSelf.Api/wwwroot',
    // 不清空：本环境 safe-delete shim 会拦截 Vite emptyDir 的删除（wrappedRmSync 抛错致构建中断）。
    // 清空改由 package.json 的 `clean` 脚本负责（用 Node fs.rm 直接删，不经 shell rm，绕开 shim）。
    emptyOutDir: false,
  },
  server: {
    // e2e（PILOT-050）：playwright.config 求值期认领的前端端口经 E2E_FRONTEND_PORT 注入；
    // 未设置（纯 pnpm run dev）保持默认 7002。strictPort 仅在 e2e 注入时开启：
    // 端口被占快速失败，而非 vite 默认的静默漂移到 port+1（那会让 e2e baseURL 失配）。
    port: process.env.E2E_FRONTEND_PORT ? Number(process.env.E2E_FRONTEND_PORT) : 7002,
    strictPort: Boolean(process.env.E2E_FRONTEND_PORT),
    host: "0.0.0.0",
    proxy: {
      '/api': {
        target: process.env.VITE_APP_BASE_API ?? process.env.E2E_BACKEND_URL ?? 'http://localhost:7102',
        changeOrigin: true
      },
      // 插件自带界面资源（spec 010）：由后端的 PluginFrontendFileMiddleware 提供。
      // 仅开发态需要代理 —— 生产环境前端产物就在后端 wwwroot 下，与 API 同源，无需转发。
      '/plugins': {
        target: process.env.VITE_APP_BASE_API ?? process.env.E2E_BACKEND_URL ?? 'http://localhost:7102',
        changeOrigin: true,
        bypass: (req) => {
          if (req.url && !/\/web\//.test(req.url)) return req.url
          return undefined
        }
      },
      '/openapi': {
        target: process.env.VITE_APP_BASE_API ?? process.env.E2E_BACKEND_URL ?? 'http://localhost:7102',
        changeOrigin: true
      },
      '/scalar': {
        target: process.env.VITE_APP_BASE_API ?? process.env.E2E_BACKEND_URL ?? 'http://localhost:7102',
        changeOrigin: true
      }
    }
  }
});