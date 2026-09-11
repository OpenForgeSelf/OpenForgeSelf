import { defineConfig, devices } from '@playwright/test'

/**
 * AIAgent 端到端执行测试专用配置。
 *
 * 与根 playwright.config.ts 的区别：
 * - **无 webServer**：本测试直连已运行的真实实例（默认 http://localhost:51888），
 *   不依赖 `pnpm run dev` 前端 dev server（本机 pnpm 不可用，且本测试走 API 驱动）。
 * - 用系统 Chrome 通道（channel: 'chrome'）兜底：本测试只用 `request` fixture（API 驱动，
 *   不启浏览器），但 Playwright 项目仍需一个浏览器标识；系统已装 Chrome 即可，无需下载 chromium。
 */
export default defineConfig({
  testDir: '.',
  timeout: 180_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  reporter: [['list']],
  use: {
    baseURL: process.env.E2E_BACKEND_URL ?? 'http://localhost:51888',
    trace: 'off',
  },
  projects: [
    {
      name: 'api',
      use: { ...devices['Desktop Chrome'], channel: 'chrome' },
    },
  ],
})
