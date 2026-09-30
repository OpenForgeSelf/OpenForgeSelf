import { defineConfig, devices } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import { claimPortSync, worktreeTag } from './e2e/helpers/free-port'

// 浏览器二进制固定落在「仓库内」<repo>/.playwright-browsers（已在根 .gitignore 忽略），
// 而非用户级缓存 ~/.cache/ms-playwright。原因：沙箱只持久化工作区，用户级缓存属临时态，
// 重置即清空 → 每次都要重下 ~180MB chromium。固定在仓库内后，重置无需再下。
// 运行与安装共用同一路径（安装见 scripts/install-browsers.mjs / pnpm run browsers:install）。
// 2026-09-28 起：三个配置统一在 projects 里加 `channel: 'chrome'` 直接用本机已装 Chrome，
// 上面这行降级为「本机无 Chrome 时」的兜底下载位置 —— 多路 worktree 各自重下 180MB 不划算。
const __dirname = dirname(fileURLToPath(import.meta.url))
process.env.PLAYWRIGHT_BROWSERS_PATH ??= resolve(__dirname, '..', '.playwright-browsers')

// ── e2e 端口同步分配（PILOT-050 T5）───────────────────────────────────────────
// Playwright 时序：webServer（vite）先于 globalSetup 启动 → 前后端端口必须在 config
// 求值期「同步」定下，globalSetup 才能把同一端口注入宿主（FORGESELF_PORT）。
// 跨 worktree 互斥：经 tmpdir 认领注册表，从默认口起顺延认领第一个可用段；
// 单 worktree 无冲突时仍拿 7002/7102（向后兼容既有行为）。
function claimE2ePort(start: number): number {
  for (let i = 0; i < 50; i++) {
    const p = start + i
    if (p > 65535) break
    if (claimPortSync(p, __dirname)) return p
  }
  throw new Error(`e2e 端口分配失败：自 ${start} 起连续 50 个端口均被认领`)
}
const FRONTEND_PORT = Number(process.env.E2E_FRONTEND_PORT) || claimE2ePort(7002)
const BACKEND_PORT = Number(process.env.E2E_BACKEND_PORT) || claimE2ePort(7102)
process.env.E2E_FRONTEND_PORT = String(FRONTEND_PORT)
process.env.E2E_BACKEND_PORT = String(BACKEND_PORT)
process.env.E2E_BACKEND_URL ??= `http://localhost:${BACKEND_PORT}`
process.env.E2E_FRONTEND_URL ??= `http://localhost:${FRONTEND_PORT}`

// MCP 中心（034 v2.0.0）e2e：为 e2e 宿主分配独立 MCP 端口，避免与常驻实例（51888 走 config.json 端口 18890）
// 的默认端口 18889 冲突。环境变量名保留 v1.0.0 旧名 FORGESELF_MCP_GATEWAY_PORT（兼容决策）。
// 多 worktree 并行会互抢固定 18889 → 默认改按 worktree 哈希派生（19000-19899，避开 18889/18890 段）。
const wtHash = parseInt(worktreeTag(__dirname).slice(3), 16)
process.env.FORGESELF_MCP_GATEWAY_PORT ??= String(19000 + (wtHash % 900))

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
    baseURL: `http://localhost:${FRONTEND_PORT}`,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], channel: 'chrome' },
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
    url: `http://localhost:${FRONTEND_PORT}`,
    reuseExistingServer: !process.env.CI,
    // vite 冷启（tailwind/组件按需预构建）在低配机上可能超默认 60s
    timeout: 120_000,
    env: {
      // vite 子进程据此绑端口并把代理指向 e2e 宿主（vite.config.ts 消费）
      E2E_FRONTEND_PORT: String(FRONTEND_PORT),
      E2E_BACKEND_URL: `http://localhost:${BACKEND_PORT}`,
    },
  },
})