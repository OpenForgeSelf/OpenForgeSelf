import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));

// 浏览器二进制固定在仓库内 <repo>/.playwright-browsers，跨沙箱重置存活，避免重复下载。
// 与 playwright.config.ts 保持一致，详见该文件说明。
process.env.PLAYWRIGHT_BROWSERS_PATH ??= resolve(__dirname, '..', '.playwright-browsers');

const root = resolve(__dirname, '..');
// [PILOT-050 T8 修复] 原写法 resolve(root, '../publish') 越级到仓库父目录（本机不存在），
// 导致 webServer 起不来；正确目标是仓库根的 publish/（build.ps1 产物）。
const publishDir = resolve(root, 'publish');
const backendExe = resolve(publishDir, 'ForgeSelf.exe');
// 端口来源：E2E_BACKEND_URL（e2e 动态端口注入）→ 默认 7102（向后兼容）
const BACKEND_URL = process.env.E2E_BACKEND_URL ?? 'http://localhost:7102';

/**
 * Playwright 发布模式 E2E 测试配置。
 *
 * 与默认 playwright.config.ts 的区别：
 * - webServer 直接启动发布后的后端 exe，不依赖前端 dev server
 * - 仅 chromium（发布模式不需要跨浏览器验证路由行为）
 * - baseURL 指向后端（E2E_BACKEND_URL ?? 7102）
 * - 仅运行 spa-fallback 相关测试
 *
 * 前置条件：
 *   运行前需先执行 `.\build.ps1` 完成打包，确保 publish/ 目录存在。
 *
 * 运行方式：
 *   pnpm run test:e2e:published
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/spa-fallback.spec.ts',
  fullyParallel: false,
  retries: 1,
  workers: 1,
  reporter: [
    ['html', { outputFolder: 'playwright-report-published' }],
    ['list'],
  ],
  use: {
    baseURL: BACKEND_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], channel: 'chrome' },
    },
  ],
  // 后端由外部启动（手动或 CI 前置步骤），
  // Playwright 不管理后端进程生命周期。
  // 启动方式：cd publish && .\ForgeSelf.exe --console
  webServer: {
    command: `"${backendExe}" --console`,
    url: `${BACKEND_URL}/api/health`,
    reuseExistingServer: true,
    timeout: 30000,
    cwd: publishDir,
  },
});
