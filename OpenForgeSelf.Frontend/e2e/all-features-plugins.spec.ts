import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 全部功能 + 插件子页 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting
 *
 * 覆盖范围：
 * - /all-features 全部功能页（功能卡片网格渲染）
 * - /plugins/updates 插件更新页
 * - /plugins/import-export 插件导入导出页
 * - /plugins/scaffolder 插件开发脚手架页
 */

// ============================================================
// 辅助函数
// ============================================================

/** 打开指定页面并等待标题 */
async function gotoAndExpectTitle(page: Page, path: string, title: string): Promise<void> {
  await page.goto(path)
  await expect(page.locator('h1, .page-title').first()).toBeVisible({ timeout: 10000 })
  await expect(page.locator('h1, .page-title').first()).toContainText(title)
}

// ============================================================
// 测试用例
// ============================================================

test.describe('全部功能与插件子页', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('全部功能页：功能卡片网格渲染', async ({ page }) => {
    await page.goto('/all-features')
    await expect(page.locator('.page-title').first()).toContainText('所有功能')
    // 功能卡片网格（真实渲染 9 个功能入口）
    const cards = page.locator('.feature-card')
    await expect(cards.first()).toBeVisible({ timeout: 10000 })
    expect(await cards.count()).toBeGreaterThanOrEqual(3)
  })

  test('插件更新页加载', async ({ page }) => {
    await gotoAndExpectTitle(page, '/plugins/updates', '插件更新')
  })

  test('插件导入导出页加载', async ({ page }) => {
    await gotoAndExpectTitle(page, '/plugins/import-export', '插件导入导出')
  })

  test('插件开发脚手架页加载', async ({ page }) => {
    await gotoAndExpectTitle(page, '/plugins/scaffolder', '插件开发脚手架')
  })
})
