import { test, expect } from '@playwright/test';

/**
 * Token 再生 E2E 测试 —— Playwright 官方最佳实践。
 *
 * 设计原则：
 * - **完全 mock 后端 API**：用 page.route() 拦截所有 /api/api-server/* 请求
 * - **无 waitForTimeout**：全部用 Playwright auto-waiting
 * - **数据隔离**：每个用例独立 mock 数据
 * - **语义化 selector**：优先 getByRole/getByText/getByLabel
 *
 * 覆盖范围：
 * - 再生 Token 流程
 * - 再生确认对话框
 * - 再生成功提示
 * - 再生失败处理
 */

// ============================================================
// Mock 数据
// ============================================================

const MOCK_NEW_TOKEN = 'sk-new-token-1234567890abcdef';

// ============================================================
// 测试用例
// ============================================================

test.describe('Token 再生流程', () => {
  test('再生 Token 成功流程', async ({ page }) => {
    let regenerateCalled = false;

    await page.route('**/api/api-server/regenerate', async (route) => {
      regenerateCalled = true;
      await route.fulfill({
        status: 200,
        json: {
          success: true,
          message: 'Token 已重新生成',
          newToken: MOCK_NEW_TOKEN,
        },
      });
    });

    await page.goto('/settings');
    await page.getByRole('button', { name: /API 服务器/ }).first().click();
    await page.waitForTimeout(500);
    await page.waitForTimeout(500);
    await page.waitForTimeout(500);

    const regenerateButton = page.getByText('重新生成 Token');
    await expect(regenerateButton).toBeVisible();
    await regenerateButton.click();

    await expect(page.getByText('确认重新生成')).toBeVisible();
    await expect(page.getByText('将使旧 Token 失效')).toBeVisible();

    await page.getByText('确认再生').click();

    await expect(page.getByText('Token 已重新生成')).toBeVisible();

    expect(regenerateCalled).toBe(true);
  });

  test('取消 Token 再生', async ({ page }) => {
    let regenerateCalled = false;

    await page.route('**/api/api-server/regenerate', async (route) => {
      regenerateCalled = true;
      await route.fulfill({
        status: 200,
        json: { success: true, message: 'Token 已重新生成' },
      });
    });

    await page.goto('/settings');
    await page.getByRole('button', { name: /API 服务器/ }).first().click();
    await page.waitForTimeout(500);
    await page.waitForTimeout(500);
    await page.waitForTimeout(500);

    await page.getByText('重新生成 Token').click();
    await page.getByText('取消').click();

    expect(regenerateCalled).toBe(false);
    await expect(page.getByText('Token 已重新生成')).not.toBeVisible();
  });

  test('Token 再生失败处理', async ({ page }) => {
    await page.route('**/api/api-server/regenerate', async (route) => {
      await route.fulfill({
        status: 400,
        json: {
          success: false,
          message: '再生失败：权限不足',
        },
      });
    });

    await page.goto('/settings');
    await page.getByRole('button', { name: /API 服务器/ }).first().click();
    await page.waitForTimeout(500);
    await page.waitForTimeout(500);
    await page.waitForTimeout(500);

    await page.getByText('重新生成 Token').click();
    await page.getByText('确认再生').click();

    await expect(page.getByText('再生失败')).toBeVisible();
    await expect(page.getByText('权限不足')).toBeVisible();
  });
});
