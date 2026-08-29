import { test, expect } from '@playwright/test';
import { injectRealApiKey, getRealApiKey, clearRealApiKeyCache } from './helpers/real-auth';

/**
 * Token 再生 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 * - **串行执行**：真实 regenerate 会轮换密钥（旧密钥立即失效），
 *   且会更新 ForgeSetting.config，故本文件内用例串行执行
 * - **副作用提示**：成功用例会真实轮换 API 密钥。运行全套 e2e 时
 *   建议串行（--workers=1）或单独运行本文件，避免与其他用例的注入密钥竞态。
 *
 * 覆盖范围：
 * - 再生 Token 成功流程（真实 POST /api/api-server/regenerate → 200）
 * - 再生成功后 localStorage 更新为新密钥，且新密钥可真实认证 status
 * - 无认证时再生失败（真实 401 → 前端提示认证失败）
 *
 * 注意：真实 ApiServerPanel 的「重新生成」按钮无确认弹窗，点击即发起请求；
 * 原 mock 版的「确认再生/取消」交互为虚构，已按真实 UI 重写。
 */

// ============================================================
// 测试用例
// ============================================================

test.describe('Token 再生流程（真实后端）', () => {
  test.describe.configure({ mode: 'serial' });

  test('再生 Token 成功：真实 regenerate 返回 200，localStorage 更新且新密钥可认证', async ({ page }) => {
    const oldKey = getRealApiKey();
    await injectRealApiKey(page);

    await page.goto('/settings');
    await page.getByRole('button', { name: /API 服务器/ }).first().click();
    await expect(page.getByText('端口配置')).toBeVisible();

    // 点击「重新生成」（真实按钮，无确认弹窗）→ 真实 POST regenerate
    const regResponsePromise = page.waitForResponse(
      (resp) => resp.url().includes('/api/api-server/regenerate') && resp.request().method() === 'POST'
    );
    await page.getByRole('button', { name: '重新生成' }).click();
    const regResponse = await regResponsePromise;

    expect(regResponse.status()).toBe(200);

    // 成功提示
    await expect(page.getByText('密钥已重新生成')).toBeVisible();

    // localStorage 已更新为新密钥（不再是旧密钥）
    const stored = await page.evaluate(() => localStorage.getItem('forge_api_token'));
    expect(stored).not.toBe(oldKey);
    expect(stored).toMatch(/^sk-/);

    // 新密钥可真实认证 status（旧密钥已失效，新密钥必须可用）
    const statusRes = await fetch('http://localhost:7102/api/api-server/status', {
      headers: { Authorization: `Bearer ${stored}` },
    });
    expect(statusRes.status).toBe(200);

    // 密钥已轮换 → 清除解密缓存，让后续用例从配置文件读取新密钥
    clearRealApiKeyCache();
  });

  test('无认证时再生失败：真实 401 → 前端提示认证失败', async ({ page }) => {
    // 不注入 token → 无 Authorization 头 → regenerate 返回 401
    await page.addInitScript(() => localStorage.removeItem('forge_api_token'));

    await page.goto('/settings');
    await page.getByRole('button', { name: /API 服务器/ }).first().click();
    await expect(page.getByText('端口配置')).toBeVisible();

    const regResponsePromise = page.waitForResponse(
      (resp) => resp.url().includes('/api/api-server/regenerate') && resp.request().method() === 'POST'
    );
    await page.getByRole('button', { name: '重新生成' }).click();
    const regResponse = await regResponsePromise;

    // 真实后端无认证 → 401
    expect(regResponse.status()).toBe(401);

    // 前端捕获 401 → 提示认证失败，并出现认证失败警告
    await expect(page.getByText('认证失败，请检查 API 密钥是否正确')).toBeVisible();
    await expect(page.getByText(/API 密钥验证失败/)).toBeVisible();
  });
});
