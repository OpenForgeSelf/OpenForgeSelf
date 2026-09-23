import { defineConfig, devices } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

// 浏览器二进制固定落在「仓库内」<repo>/.playwright-browsers（已在根 .gitignore 忽略），
// 而非用户级缓存 ~/.cache/ms-playwright。原因：沙箱只持久化工作区，用户级缓存属临时态，
// 重置即清空 → 每次都要重下 ~180MB chromium。固定在仓库内后，重置无需再下。
// 运行与安装共用同一路径（安装见 scripts/install-browsers.mjs / pnpm run browsers:install）。
const __dirname = dirname(fileURLToPath(import.meta.url))
process.env.PLAYWRIGHT_BROWSERS_PATH ??= resolve(__dirname, '..', '.playwright-browsers')

// MCP 中心（034 v2.0.0）e2e：为 e2e 宿主分配独立 MCP 端口，避免与常驻实例（51888 走 config.json 端口 18890）
// 的默认端口 18889 冲突。环境变量名保留 v1.0.0 旧名 FORGESELF_MCP_GATEWAY_PORT（兼容决策）。
// globalSetup 与各 worker 均继承本环境变量，宿主与用例看到同一端口。
process.env.FORGESELF_MCP_GATEWAY_PORT ??= '18889'

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
    // firefox / webkit 为可选矩阵：需先 `playwright install firefox webkit`。
    // 项目既有约束为「e2e 仅 chromium 可用」（见 specs/033-home/design.md §13），
    // 故默认矩阵收敛为 chromium，避免未安装浏览器二进制导致的 launch error。
    // 若后续补齐 firefox/webkit 二进制，解开下方注释即可恢复三矩阵。
    // {
    //   name: 'firefox',
    //   use: { ...devices['Desktop Firefox'] },
    // },
    // {
    //   name: 'webkit',
    //   use: { ...devices['Desktop Safari'] },
    // },
  ],
  webServer: {
    command: 'pnpm run dev',
    url: 'http://localhost:7002',
    reuseExistingServer: !process.env.CI,
  },
})