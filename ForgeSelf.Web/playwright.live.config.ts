import { defineConfig, devices } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

// 浏览器二进制固定在仓库内 <repo>/.playwright-browsers，跨沙箱重置存活，避免重复下载。
// 与 playwright.config.ts 保持一致，详见该文件说明。
const __dirname = dirname(fileURLToPath(import.meta.url))
process.env.PLAYWRIGHT_BROWSERS_PATH ??= resolve(__dirname, '..', '.playwright-browsers')

/**
 * 轻量 e2e 配置：用于「验证已在运行的宿主（如 51888 生产实例）」的插件走查。
 * 不走 globalSetup（不另起全新宿主），也不起 webServer（直接用运行中的宿主）。
 *
 * 运行：
 *   $env:E2E_API_TOKEN="<解密明文>"   # 或 $env:FORGE_SETTING_CONFIG="~/.forgeself/Config/ForgeSetting.config"
 *   pnpm exec playwright test --config=playwright.live.config.ts e2e/plugins/quick-links
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:51888',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
