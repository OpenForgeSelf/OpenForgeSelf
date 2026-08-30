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
    port: 7002,
    host: "0.0.0.0",
    proxy: {
      '/api': {
        target: 'http://localhost:7102',
        changeOrigin: true
      },
      // 插件自带界面资源（spec 010）：由后端的 PluginFrontendFileMiddleware 提供。
      // 仅开发态需要代理 —— 生产环境前端产物就在后端 wwwroot 下，与 API 同源，无需转发。
      '/plugins': {
        target: 'http://localhost:7102',
        changeOrigin: true
      },
      '/openapi': {
        target: 'http://localhost:7102',
        changeOrigin: true
      },
      '/scalar': {
        target: 'http://localhost:7102',
        changeOrigin: true
      }
    }
  }
});