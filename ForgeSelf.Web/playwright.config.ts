import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  // 统一 e2e 共享基础设施：globalSetup 自动构建宿主到临时目录 + 起宿主(7102) + 解密 token；
  // teardown 只杀本轮回合拉起的宿主进程。前端 dev(7002) 由下方 webServer 拉起。
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:7002',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: {
    command: 'pnpm run dev',
    url: 'http://localhost:7002',
    reuseExistingServer: !process.env.CI,
  },
})