import { test, expect } from '@playwright/test'

/**
 * 首页冒烟测试 —— 验证 / 首页核心区域渲染与推荐动作跳转。
 *
 * 设计原则（对齐 Playwright 官方最佳实践）：
 * - ✅ 语义化 Locator：getByRole / getByText / getByPlaceholder，避免宽泛 CSS selector
 * - ✅ 无 waitForTimeout：全部用 auto-waiting（expect(locator).toBeVisible()）
 * - ✅ 无状态依赖：每个用例独立，无共享状态
 * - ✅ Mock 依赖 API（/api/skills、/api/todos），不依赖真实后端
 */

/** 包装为后端 ApiResponse<T> 成功响应体 */
function ok<T>(data: T) {
  return { code: 0, message: 'ok', success: true, data }
}

test.describe('首页 /', () => {
  test.beforeEach(async ({ page }) => {
    // 首页 homeStore.init() 会拉取已启用技能数与待办列表，统一 mock 为空数据
    await page.route('**/api/skills**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok([])),
      })
    )
    await page.route('**/api/todos**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(ok({ items: [], total: 0, page: 1, pageSize: 20 })),
      })
    )
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
