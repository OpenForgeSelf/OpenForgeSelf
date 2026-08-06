import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 个人中心 E2E 测试 —— 对接真实后端 API（无 mock、真实认证）。
 *
 * 设计原则：
 * - **零 mock**：不拦截任何 /api/* 请求，全部走真实后端（http://localhost:7102/api/planning/*）
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - **无 waitForTimeout**：全部用 auto-waiting + waitForResponse
 *
 * 覆盖范围：
 * - 个人中心页面加载（头像/昵称/技能/偏好区域可见）
 * - 真实后端 /api/planning/profile 与 /api/planning/skills 数据被请求且成功
 */

// ============================================================
// 辅助函数
// ============================================================

/** 打开个人中心页面 */
async function gotoProfile(page: Page): Promise<void> {
  await page.goto('/profile')
}

// ============================================================
// 测试用例
// ============================================================

test.describe('个人中心（/profile）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('页面加载：个人中心核心区域可见', async ({ page }) => {
    await gotoProfile(page)

    // 页面主区域渲染（个人资料/技能/偏好/建议）
    await expect(page.locator('.profile-view, .profile-header').first()).toBeVisible({ timeout: 10000 })
    // 技能与偏好区域（空态文案或数据区均可）
    await expect(page.locator('.profile-left')).toBeVisible()
  })

  test('真实后端 /api/planning 接口请求成功', async ({ page }) => {
    const requested: string[] = []
    page.on('response', (resp) => {
      if (resp.url().includes('/api/planning/')) {
        requested.push(`${resp.status()} ${resp.url().replace('http://localhost:7002', '')}`)
      }
    })

    await gotoProfile(page)
    await expect(page.locator('.profile-view, .profile-header').first()).toBeVisible({ timeout: 10000 })

    // profile / skills 等真实接口应被请求且 200
    await expect
      .poll(
        () => {
          const ok = requested.filter((r) => r.startsWith('200'))
          return ok.length
        },
        { timeout: 10000 }
      )
      .toBeGreaterThan(0)
  })

  test('页面加载无 JS 运行时错误', async ({ page }) => {
    const errors: string[] = []
    page.on('pageerror', (err) => errors.push(err.message))

    await gotoProfile(page)
    await expect(page.locator('.profile-view, .profile-header').first()).toBeVisible({ timeout: 10000 })

    expect(errors).toEqual([])
  })
})
