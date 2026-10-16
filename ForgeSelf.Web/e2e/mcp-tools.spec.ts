import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * MCP 中心（插件 mcp-center）工具管理 E2E 冒烟 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围（主链路冒烟）：
 * - MCP 服务器列表加载（真实后端播种的服务器）
 * - 选择服务器 → 工具列表加载（真实工具数据）
 * - 工具搜索过滤
 * - 测试工具（真实 POST test）
 *
 * 2026-10-16：视图已随 034 v2.0.0 从宿主 /mcp-tools 迁入插件 /mcp-center（McpCenterView，
 * 页签「工具管理」沿用 .server-item/.table-row 结构），本 spec 同步改指插件路由。
 */

// ============================================================
// 辅助函数
// ============================================================

/** 打开 MCP 中心（插件）页面并等待工具管理页签渲染 */
async function gotoMcpCenter(page: Page): Promise<void> {
  await page.goto('/mcp-center')
  await expect(page.getByRole('heading', { name: 'MCP 中心' })).toBeVisible()
}

// ============================================================
// 测试用例
// ============================================================

test.describe('MCP 中心·工具管理（/mcp-center 插件）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('服务器列表加载：真实后端播种的服务器可见', async ({ page }) => {
    await gotoMcpCenter(page)

    // 真实后端至少有 Database/Filesystem 等服务器（server-name 唯一，tag 有重复）
    await expect(page.locator('.server-item').filter({ hasText: 'Database' }).first()).toBeVisible({ timeout: 10000 })
    await expect(page.locator('.server-item').filter({ hasText: 'Filesystem' }).first()).toBeVisible()
    // 服务器计数徽标
    await expect(page.getByText(/个工具/).first()).toBeVisible()
  })

  test('选择服务器后工具列表加载：真实工具数据可见', async ({ page }) => {
    await gotoMcpCenter(page)
    await expect(page.getByText('Filesystem', { exact: true })).toBeVisible({ timeout: 10000 })

    // 点击 Filesystem 服务器 → 工具列表加载
    await page.locator('.server-item').filter({ hasText: 'Filesystem' }).click()
    await expect(page.getByText('list_directory', { exact: true })).toBeVisible({ timeout: 10000 })
    await expect(page.getByText('read_file', { exact: true })).toBeVisible()
  })

  test('工具搜索过滤：按名称本地过滤', async ({ page }) => {
    await gotoMcpCenter(page)
    await expect(page.getByText('Filesystem', { exact: true })).toBeVisible({ timeout: 10000 })
    await page.locator('.server-item').filter({ hasText: 'Filesystem' }).click()
    await expect(page.getByText('list_directory', { exact: true })).toBeVisible({ timeout: 10000 })

    const searchInput = page.getByPlaceholder(/搜索工具/)
    await searchInput.fill('read_file')
    await expect(page.getByText('read_file', { exact: true })).toBeVisible()
  })

  test('测试工具：真实 POST test 后显示结果', async ({ page }) => {
    await gotoMcpCenter(page)
    await expect(page.getByText('Filesystem', { exact: true })).toBeVisible({ timeout: 10000 })
    await page.locator('.server-item').filter({ hasText: 'Filesystem' }).click()
    await expect(page.getByText('list_directory', { exact: true })).toBeVisible({ timeout: 10000 })

    // 找到第一行的测试按钮并点击 → 真实 POST /api/mcp/tools/{id}/test
    const firstRow = page.locator('.table-row').first()
    const testBtn = firstRow.getByRole('button', { name: '测试' })
    await expect(testBtn).toBeVisible()

    const testResponsePromise = page.waitForResponse(
      (resp) =>
        resp.request().method() === 'POST' && /\/api\/mcp\/tools\/.+\/test/.test(resp.url())
    )
    await testBtn.click()
    await testResponsePromise

    // 测试结果 toast 出现（成功或失败均展示）
    await expect(page.locator('.test-toast-overlay')).toBeVisible({ timeout: 10000 })
  })
})
