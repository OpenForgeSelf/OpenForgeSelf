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

  test('插件管理卡片提供「插件详情」+「插件页面」双入口（新增页面入口，不取代详情）', async ({ page }) => {
    await page.goto('/all-features')
    await expect(page.locator('.page-title').first()).toContainText('所有功能')

    // 定位「插件管理」卡片：两个下拉入口并存
    const card = page.locator('.feature-card').filter({ hasText: '插件管理' })
    await expect(card).toBeVisible({ timeout: 10000 })
    const detailEntry = card.getByRole('button', { name: /插件详情/ })
    const pageEntry = card.getByRole('button', { name: /插件页面/ })
    await expect(detailEntry).toBeVisible()
    await expect(pageEntry).toBeVisible()

    // ① 插件详情：点第一项 → /plugins/<id>（PluginDetail）
    await detailEntry.click()
    const detailMenu = page.locator('.el-dropdown-menu:visible')
    await expect(detailMenu).toBeVisible()
    const items = detailMenu.locator('.el-dropdown-menu__item')
    expect(await items.count()).toBeGreaterThan(0)
    await items.first().click()
    await page.waitForURL(/\/plugins\//, { timeout: 10000 })

    // ② 插件页面：点第一项 → 离开 /plugins 直达插件 route
    await page.goto('/all-features')
    await expect(card).toBeVisible({ timeout: 10000 })
    await pageEntry.click()
    const pageMenu = page.locator('.el-dropdown-menu:visible')
    await expect(pageMenu).toBeVisible()
    expect(await pageMenu.locator('.el-dropdown-menu__item').count()).toBeGreaterThan(0)
    await pageMenu.locator('.el-dropdown-menu__item').first().click()
    await page.waitForURL((u) => u.pathname !== '/all-features', { timeout: 10000 })
    expect(new URL(page.url()).pathname).not.toBe('/all-features')
  })
})
