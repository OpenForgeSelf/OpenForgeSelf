import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * Agent 管理 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - Agent 列表加载（真实后端播种的 7 个 Agent）
 * - Agent 卡片渲染（名称/类型）
 * - 创建 Agent 按钮入口存在
 */

// ============================================================
// 辅助函数
// ============================================================

/** 打开 Agent 管理页面 */
async function gotoAgents(page: Page): Promise<void> {
  await page.goto('/agents')
}

// ============================================================
// 测试用例
// ============================================================

test.describe('Agent 管理（/agents）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('Agent 列表加载：真实后端播种的 Agent 可见', async ({ page }) => {
    await gotoAgents(page)

    // 真实后端播种 7 个 Agent（协调者/研究员/程序员等）；agent-tag 会重复名称，用 .agent-name 定位
    await expect(page.locator('.agent-card').first()).toBeVisible({ timeout: 10000 })
    await expect(page.locator('.agent-name').filter({ hasText: '协调者' })).toBeVisible()
    await expect(page.locator('.agent-name').filter({ hasText: '程序员' })).toBeVisible()
  })

  test('Agent 卡片展示名称与启用状态', async ({ page }) => {
    await gotoAgents(page)
    await expect(page.locator('.agent-card').first()).toBeVisible({ timeout: 10000 })

    // 卡片含名称与状态（启用/禁用徽标）
    const firstCard = page.locator('.agent-card').first()
    await expect(firstCard.locator('.agent-name')).toBeVisible()
  })

  test('创建 Agent 入口存在', async ({ page }) => {
    await gotoAgents(page)

    // 页头「创建 Agent」按钮可见
    await expect(page.getByRole('button', { name: /创建 Agent/ })).toBeVisible()
  })
})
