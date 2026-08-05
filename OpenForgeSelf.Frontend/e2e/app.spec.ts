import { test, expect } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 首页冒烟测试 —— 验证 / 首页核心区域渲染与推荐动作跳转（真实后端、无 mock）。
 *
 * 设计原则（对齐 Playwright 官方最佳实践）：
 * - ✅ 零 mock：不拦截任何 /api/* 请求，首页 homeStore 数据来自真实后端
 * - ✅ 真实认证：注入 ForgeSetting.config 解密出的真实 API 密钥
 * - ✅ 语义化 Locator：getByRole / getByText / getByPlaceholder
 * - ✅ 无 waitForTimeout：全部用 auto-waiting（expect(locator).toBeVisible()）
 * - ✅ 断言不依赖具体数据量：只验证区域渲染与入口存在性，避免真实数据波动导致 flaky
 */

test.describe('首页 /（真实后端）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
    await page.goto('/')
  })

  test('页面标题为「铸己匣 - OpenForgeSelf」', async ({ page }) => {
    await expect(page).toHaveTitle(/OpenForgeSelf/)
  })

  test('Hero 区显示问候语与快速提问条', async ({ page }) => {
    await expect(page.locator('.hero-title')).toContainText('锻造师')
    await expect(page.getByPlaceholder(/想做什么/)).toBeVisible()
  })

  test('推荐动作点击后跳转到对应路由', async ({ page }) => {
    await page.getByRole('button', { name: '整理周报' }).click()
    await expect(page).toHaveURL(/\/ai-agent/)
  })

  test('常用功能网格渲染核心入口', async ({ page }) => {
    // 网格按使用频率排序，故逐个断言存在性而非顺序
    const grid = page.locator('.quick-grid')
    await expect(grid.getByText('AI Agent', { exact: true })).toBeVisible()
    await expect(grid.getByText('工作流', { exact: true })).toBeVisible()
    await expect(grid.getByText('系统监控', { exact: true })).toBeVisible()
    await expect(grid.getByText('技能管理', { exact: true })).toBeVisible()
  })

  test('核心看板渲染各 Widget', async ({ page }) => {
    await expect(page.getByRole('heading', { name: '核心看板' })).toBeVisible()
    await expect(page.locator('.widget-card').filter({ hasText: 'AI Agent' })).toBeVisible()
    await expect(page.locator('.widget-card').filter({ hasText: '能力成长' })).toBeVisible()
    await expect(page.locator('.widget-card').filter({ hasText: '系统监控' })).toBeVisible()
  })

  test('右侧待办面板渲染', async ({ page }) => {
    await expect(page.locator('.todo-panel')).toBeVisible()
    await expect(page.locator('.todo-panel').getByText('待办', { exact: true })).toBeVisible()
  })

  test('首页加载无 JS 运行时错误', async ({ page }) => {
    const errors: string[] = []
    page.on('pageerror', err => errors.push(`pageerror: ${err.message}`))

    await page.reload()
    await expect(page.locator('.hero-title')).toBeVisible()

    expect(errors).toEqual([])
  })
})
