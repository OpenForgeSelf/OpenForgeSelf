import { test, expect } from '@playwright/test';
import { injectRealApiKey, getRealApiKey } from './helpers/real-auth';

/**
 * Token 首次初始化 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 Playwright auto-waiting + waitForResponse
 * - **条件断言**：验收点"只要 init-token 返回 200 并有 token，必验证 token 已存储，
 *   且页面上正常展示 API 服务地址"——测试读取 init-token 真实响应后按分支断言
 *
 * 覆盖范围：
 * - 首次打开设置页且本地无 token 时，自动请求后端 init-token 一次性接口（真实请求）
 * - init-token 返回 200 且有 token → 验证 token 已保存到 localStorage + 地址正常展示
 *   （真实后端首次初始化完成后返回 success=false、无 token → 验证不存储 + 401 提示分支）
 * - 注入真实 token 后 status 返回 200 → 页面正常展示 API 服务地址
 */

// ============================================================
// 测试用例
// ============================================================

test.describe('Token 首次初始化（真实后端）', () => {
  test('无 token 首次打开：真实请求 init-token，按响应分支验证存储与展示', async ({ page }) => {
    // 确保初始无 token（独立初始状态）
    await page.addInitScript(() => localStorage.removeItem('forge_api_token'));

    // 捕获 init-token 真实响应（不拦截，仅监听）
    const initTokenResponsePromise = page.waitForResponse(
      (resp) => resp.url().includes('/api/api-server/init-token') && resp.status() === 200
    );

    await page.goto('/settings');
    await page.getByRole('button', { name: 'API 服务器' }).click();

    // 面板加载完成
    await expect(page.getByText('端口配置')).toBeVisible();

    // init-token 请求真实发出且返回 200
    const initTokenResponse = await initTokenResponsePromise;
    expect(initTokenResponse.status()).toBe(200);

    // 读取真实响应体，按分支验证
    const body = (await initTokenResponse.json()) as {
      success: boolean;
      data: { apiKeyPlain?: string } | null;
    };

    if (body.success && body.data?.apiKeyPlain) {
      // 分支 1：init-token 返回 200 且有 token → 必验证 token 已存储 + 地址展示
      const stored = await page.evaluate(() => localStorage.getItem('forge_api_token'));
      expect(stored).toBe(body.data!.apiKeyPlain!);
      await expect(page.getByText(`${process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'}/v1`)).toBeVisible();
    } else {
      // 分支 2：真实后端已初始化（success=false 无 token）→ 不存储，页面走 401 认证提示
      const stored = await page.evaluate(() => localStorage.getItem('forge_api_token'));
      expect(stored).toBeNull();
      await expect(page.getByText(/API 密钥验证失败/)).toBeVisible();
    }
  });

  test('注入真实 token 后 status 返回 200，页面正常展示 API 服务地址', async ({ page }) => {
    await injectRealApiKey(page);

    await page.goto('/settings');
    await page.getByRole('button', { name: 'API 服务器' }).click();

    // 真实认证通过：status 返回配置，端口配置卡片可见
    await expect(page.getByText('端口配置')).toBeVisible();
    // 页面上正常展示 API 服务地址（真实后端返回 apiBaseUrl）
    await expect(page.getByText(`${process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'}/v1`)).toBeVisible();
    // 密钥已存在（真实密钥注入生效）
    const stored = await page.evaluate(() => localStorage.getItem('forge_api_token'));
    expect(stored).toBe(getRealApiKey());
  });
});
