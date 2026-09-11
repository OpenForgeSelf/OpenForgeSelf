import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  // spa-fallback.spec.ts 是「发布模式 / 生产部署」语义测试（CSS 为 <link>、JS 为哈希产物、
  // /v1/models 需鉴权返回 401），其断言只在 dotnet publish 后的产物 + 内置 wwwroot 下成立，
  // 由 playwright.e2e-published.config.ts 专属驱动。默认 dev 配置用 Vite dev server 跑它会
  // 因 dev 注入 CSS 为 <style>、多 script[src] 跳转 detach、dev 鉴权差异而失败，故此处显式排除，
  // 避免污染默认 e2e 门禁（prod 验证仍走 `pnpm run test:e2e:published`）。
  testIgnore: ['**/spa-fallback.spec.ts'],
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