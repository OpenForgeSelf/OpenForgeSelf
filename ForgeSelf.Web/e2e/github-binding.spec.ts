import { test, expect, type Page } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * GitHub 账号绑定域 E2E（PILOT-083 + 绑定 UX 两轮）—— 真实后端（零外联）。
 *
 * 设计原则：
 * - **零外联**：global-setup 步骤 1.4 已把 publish 宿主的 GitHubOAuth:ClientId 置空，
 *   bind/start 走「未配置」分支（HTTP 400 + 明确错误文案），全程不触 github.com。
 * - **真实认证**：注入 ForgeSetting.config 解密出的真实 API 密钥（页面 fetch 一律带 Bearer）。
 * - 零 mock：不拦截 /api/*。
 *
 * 覆盖：
 * - 设置页出现「GitHub 账号」分类，未绑定态有绑定入口
 * - 点击绑定 → 设备码申请失败（未配置 ClientId）→ 错误文案透传 + 重试入口（错误体透明化）
 * - API 契约：status 外壳与未绑定形状、DELETE bind 幂等（页面内带 token fetch 直验）
 */

/** 页面上下文内带真实令牌调后端（authFetch 同键 forge_api_token） */
async function apiInPage(page: Page, url: string, init: RequestInit = {}): Promise<{ status: number; body: Record<string, unknown> }> {
  return page.evaluate(async ([u, i]) => {
    const token = localStorage.getItem('forge_api_token')
    const headers = { ...((i.headers as Record<string, string>) ?? {}) }
    if (token) headers['Authorization'] = `Bearer ${token}`
    const res = await fetch(u, { ...i, headers })
    return { status: res.status, body: (await res.json()) as Record<string, unknown> }
  }, [url, init] as const)
}

async function gotoSettingsGithub(page: Page): Promise<void> {
  await page.goto('/settings?cat=github-account')
}

test.describe('GitHub 账号绑定（设置页 + API 契约）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
  })

  test('设置页含「GitHub 账号」分类且未绑定态渲染绑定入口', async ({ page }) => {
    await gotoSettingsGithub(page)
    await expect(page.locator('[data-test="github-account-panel"]')).toBeVisible({ timeout: 15000 })
    await expect(page.locator('[data-test="bind-button"]')).toBeVisible()
  })

  test('绑定入口点击：设备码申请因未配置 ClientId 而失败，错误文案透传给用户', async ({ page }) => {
    await gotoSettingsGithub(page)
    await expect(page.locator('[data-test="bind-button"]')).toBeVisible({ timeout: 15000 })
    const responsePromise = page.waitForResponse((r) => r.url().includes('/api/github-account/bind/start') && r.request().method() === 'POST')
    await page.locator('[data-test="bind-button"]').click()
    const response = await responsePromise
    // 未配置 ClientId → 400 明确错误（错误体透明化：不吞成「网络异常」）
    expect(response.status()).toBe(400)
    const body = await response.json()
    expect(body.success).toBe(false)
    expect(String(body.error)).toContain('ClientId')
    // 前端把上游原因透传进错误块 + 提供重试
    await expect(page.locator('[data-test="error-block"]')).toBeVisible({ timeout: 10000 })
    await expect(page.locator('[data-test="error-text"]')).toContainText('ClientId')
    await expect(page.locator('[data-test="retry-button"]')).toBeVisible()
  })

  test('API 契约：GET status 返回绑定态外壳（success/data.bound/autoAttachToken）', async ({ page }) => {
    await page.goto('/')
    const { status, body } = await apiInPage(page, '/api/github-account/status')
    expect(status).toBe(200)
    expect(body.success).toBe(true)
    const data = body.data as Record<string, unknown>
    expect(data).toHaveProperty('bound')
    expect(data).toHaveProperty('autoAttachToken')
  })

  test('API 契约：未绑定态下 DELETE bind 幂等返回未绑定', async ({ page }) => {
    await page.goto('/')
    const { status, body } = await apiInPage(page, '/api/github-account/bind', { method: 'DELETE' })
    expect(status).toBe(200)
    expect(body.success).toBe(true)
    const data = body.data as Record<string, unknown>
    expect(data.bound).toBe(false)
  })
})
