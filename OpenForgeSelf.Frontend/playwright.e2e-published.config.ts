import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');
const publishDir = resolve(root, '../publish');
const backendExe = resolve(publishDir, 'OpenForgeSelf.exe');

/**
 * Playwright 发布模式 E2E 测试配置。
 *
 * 与默认 playwright.config.ts 的区别：
 * - webServer 直接启动发布后的后端 exe，不依赖前端 dev server
 * - 仅 chromium（发布模式不需要跨浏览器验证路由行为）
 * - baseURL 指向后端端口 7102
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
    baseURL: 'http://localhost:7102',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  // 后端由外部启动（手动或 CI 前置步骤），
  // Playwright 不管理后端进程生命周期。
  // 启动方式：cd publish && .\OpenForgeSelf.exe --console
  webServer: {
    command: `"${backendExe}" --console`,
    url: 'http://localhost:7102/api/health',
    reuseExistingServer: true,
    timeout: 30000,
    cwd: publishDir,
  },
});
