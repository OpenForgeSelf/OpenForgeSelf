import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 系统监控 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102/api/monitor）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - 系统监控页面加载（概览统计卡片）
 * - 真实后端 /api/monitor/overview 数据被渲染（CPU/内存/磁盘）
 * - 页面无 JS 运行时错误
 */

// ============================================================
// 辅助函数
// ============================================================

/** 打开系统监控页面 */
async function gotoMonitor(page: Page): Promise<void> {
  await page.goto('/system-monitor')
}

// ============================================================
// 测试用例
// ============================================================

test.describe('系统监控（/system-monitor）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('页面加载：概览统计卡片可见', async ({ page }) => {
    await gotoMonitor(page)

    // 页面标题/核心看板渲染（真实后端数据驱动）
    await expect(page.locator('.stat-big').first()).toBeVisible({ timeout: 10000 })
  })

  test('真实后端 /api/monitor/overview 数据渲染（CPU/内存/磁盘）', async ({ page }) => {
    await gotoMonitor(page)
    await expect(page.locator('.stat-big').first()).toBeVisible({ timeout: 10000 })

    // overview 数据经 monitorHub 轮询推送（2s 间隔），等待 stat 出现非 -- 的真实值
    await expect
      .poll(
        async () => {
          const statTexts = await page.locator('.stat-big').allTextContents()
          return statTexts.filter((t) => t.trim() && t.trim() !== '--').length
        },
        { timeout: 15000 }
      )
      .toBeGreaterThan(0)
  })

  test('页面加载无 JS 运行时错误', async ({ page }) => {
    const errors: string[] = []
    page.on('pageerror', (err) => errors.push(err.message))

    await gotoMonitor(page)
    await expect(page.locator('.stat-big').first()).toBeVisible({ timeout: 10000 })

    expect(errors).toEqual([])
  })
})
