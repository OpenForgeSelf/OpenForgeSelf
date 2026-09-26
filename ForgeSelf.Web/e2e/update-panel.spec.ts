import { test, expect, Page } from '@playwright/test';
import { getRealApiKey } from './helpers/real-auth';

/**
 * 设置-版本更新 E2E（spec 036）—— 对接真实后端 /api/update。
 *
 * 设计原则：
 * - 注入真实 API 密钥（helpers/real-auth 运行时解密），零 mock
 * - 「检查更新」走真实 provider 配置（appsettings Update:Provider）；
 *   外网 GitHub 不可达/无 token 时后端返回失败信息，UI 应优雅提示而非崩溃
 * - 不做「下载/重启并更新」的真实 apply：会终止共享宿主进程，属破坏性操作，
 *   由一次性人工端到端验收覆盖（见 specs/036 验收）
 */

const REAL_API_KEY = getRealApiKey();

async function injectApiKey(page: Page) {
  await page.addInitScript((key) => {
    localStorage.setItem('forge_api_token', key);
  }, REAL_API_KEY);
}

async function openUpdatePanel(page: Page) {
  await injectApiKey(page);
  await page.goto('/settings');
  await page.getByRole('button', { name: '版本更新' }).click();
  await expect(page.getByRole('heading', { name: '版本更新' })).toBeVisible();
}

test.describe('设置 - 版本更新（spec 036）', () => {
  test('面板渲染：当前版本与检查按钮可见', async ({ page }) => {
    await openUpdatePanel(page);
    await expect(page.getByText('当前版本')).toBeVisible();
    await expect(page.getByRole('button', { name: '检查更新' })).toBeVisible();
  });

  test('status 接口回填当前版本号', async ({ page }) => {
    await openUpdatePanel(page);
    // 版本号渲染在「当前版本」卡片内（v + 非空版本串）
    await expect(
      page.locator('div').filter({ has: page.getByText('当前版本', { exact: true }) }).locator('div.font-semibold')
    ).toContainText(/^v\d+\.\d+/, { timeout: 10000 });
  });

  test('点击检查更新：出现结果提示且页面不崩溃', async ({ page }) => {
    await openUpdatePanel(page);
    await page.getByRole('button', { name: '检查更新' }).click();

    // 三种合法结局：发现新版本 / 已是最新版本 / 检查失败提示（网络或 token 问题）
    await expect(
      page
        .locator('text=发现新版本')
        .or(page.locator('text=已是最新版本'))
        .or(page.locator('text=检查更新失败'))
        .or(page.locator('text=未配置'))
        .first()
    ).toBeVisible({ timeout: 45000 });

    // 面板仍可用（无未捕获异常导致的白屏）
    await expect(page.getByRole('button', { name: '检查更新' })).toBeVisible();
  });
});
