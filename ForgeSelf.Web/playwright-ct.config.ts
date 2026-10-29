import { defineConfig, devices } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

/**
 * 组件视觉回归（CT）专用 Playwright 配置 —— 与 e2e 的 `playwright.config.ts` 完全解耦。
 *
 * 与 e2e 的区别（为什么要单独一份）：
 *   - 不起后端、不登录、不注入 token：画廊是纯前端静态页面，组件状态由 story 的 props 决定；
 *   - 不用 globalSetup/teardown：没有宿主进程要构建和回收；
 *   - 断言目标是像素基线（`toHaveScreenshot`）而非用户主链路。
 *
 * 端口：默认 5190；`CT_FRESH=1` 时换 5193 且不复用既有 server
 * （本机常有陈旧 vite 占着 5190，表现为「改了源码截图不变」的假象）。
 */
const __dirname = dirname(fileURLToPath(import.meta.url))

// 与 e2e 配置共用「浏览器二进制落在仓库内」的策略（本机有 Chrome 时走 channel，不下这份）
process.env.PLAYWRIGHT_BROWSERS_PATH ??= resolve(__dirname, '..', '.playwright-browsers')

const fresh = process.env.CT_FRESH === '1'
const port = Number(process.env.CT_PORT) || (fresh ? 5193 : 5190)

export default defineConfig({
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: 'list',
  use: {
    baseURL: `http://127.0.0.1:${port}/`,
    // 固定视口：组件宽度/换行依赖视口，不固定会让基线随窗口大小漂移
    viewport: { width: 1000, height: 800 },
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'ct-host',
      // 故事与用例都放在组件旁边。跨包组件（如 AIAgent 插件聊天消息）由宿主薄壳承接，
      // 用例仍落在本目录 —— 插件目录没有 @playwright/test，也不该为测试新增依赖。
      testDir: './src/components',
      testMatch: '**/*.ct.spec.ts',
      use: { ...devices['Desktop Chrome'], channel: 'chrome' },
    },
  ],
  webServer: {
    // 直接用 node 跑 vite（不经 pnpm，避免多一层进程导致 kill 不干净）
    command: 'node node_modules/vite/bin/vite.js --config ct/vite.config.ts',
    cwd: __dirname,
    url: `http://127.0.0.1:${port}/`,
    reuseExistingServer: fresh ? false : !process.env.CI,
    timeout: 180_000,
    env: { CT_PORT: String(port) },
  },
})
