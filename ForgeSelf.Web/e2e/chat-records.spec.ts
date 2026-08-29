import { test, expect } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 聊天记录页 E2E（真实后端、零 mock）。
 *
 * 对齐项目约定（参见 e2e/app.spec.ts）：
 * - 真实认证：beforeEach 注入 forge_api_token，聊天记录 API 走真实后端
 * - 语义化 Locator：getByRole / getByText / locator
 * - 无 waitForTimeout：全部用 auto-waiting（expect(locator).toBeVisible()）
 * - 断言不依赖具体数据量：只验证区域渲染与关键结构，避免真实数据波动导致 flaky
 */

test.describe('聊天记录列表页', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
    await page.goto('/chat-records')
  })

  test('页面标题与根容器渲染', async ({ page }) => {
    await expect(page.locator('.chat-records-view')).toBeVisible()
    await expect(page.locator('.view-title')).toContainText('聊天记录查看')
  })

  test('筛选栏包含 SessionId / Style / 搜索 / 重置', async ({ page }) => {
    await expect(page.locator('.filter-bar')).toBeVisible()
    // SessionId 为第一个文本输入框
    await expect(page.locator('.filter-input').first()).toBeVisible()
    await expect(page.locator('.filter-select')).toBeVisible()
    await expect(page.locator('.search-btn')).toBeVisible()
    await expect(page.locator('.reset-btn')).toBeVisible()
  })

  test('记录表格渲染（有数据时含行，无数据时含空态）', async ({ page }) => {
    await expect(page.locator('.chat-records-list')).toBeVisible()
    await expect(page.locator('.records-table')).toBeVisible()
    // 表格体要么有数据行，要么显示空态，二者必居其一
    const hasRows = (await page.locator('.record-row').count()) > 0
    const hasEmpty = (await page.locator('.empty-row').count()) > 0
    expect(hasRows || hasEmpty).toBeTruthy()
  })

  test('分页控件存在', async ({ page }) => {
    await expect(page.locator('.pagination')).toBeVisible()
  })
})

test.describe('聊天记录详情面板', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
    await page.goto('/chat-records')
  })

  test('点击「查看详情」打开详情并渲染响应区', async ({ page }) => {
    // 等列表行出现（API 真实返回）
    await expect(page.locator('.record-row').first()).toBeVisible({ timeout: 15000 })
    await page.locator('.record-row .detail-btn').first().click()

    // 详情面板与 ChatRecordDetail 根容器出现（auto-waiting 覆盖 API 拉取延迟）
    await expect(page.locator('.detail-section')).toBeVisible()
    await expect(page.locator('.chat-record-detail')).toBeVisible()

    // 响应区始终渲染：含 Finish Reason 标签与 usage（网格或空占位二选一）
    await expect(page.getByText('响应', { exact: true })).toBeVisible()
    await expect(page.locator('.info-label', { hasText: 'Finish Reason' })).toBeVisible()
    await expect(page.locator('.usage-grid, .usage-empty').first()).toBeVisible()
  })

  test('工具调用渲染时必带执行结果（验证请求/响应归一化关联）', async ({ page }) => {
    await expect(page.locator('.record-row').first()).toBeVisible({ timeout: 15000 })
    await page.locator('.record-row .detail-btn').first().click()
    await expect(page.locator('.chat-record-detail')).toBeVisible()

    const toolCallCount = await page.locator('.chat-record-detail .tool-call-item').count()
    if (toolCallCount > 0) {
      // 工具调用块存在时，应渲染「执行结果」子块（关联逻辑）
      await expect(page.locator('.chat-record-detail .tool-call-result').first()).toBeVisible()
    }
  })

  test('关闭按钮可收起详情面板', async ({ page }) => {
    await expect(page.locator('.record-row').first()).toBeVisible({ timeout: 15000 })
    await page.locator('.record-row .detail-btn').first().click()
    await expect(page.locator('.detail-section')).toBeVisible()

    await page.locator('.close-btn').click()
    await expect(page.locator('.detail-section')).toHaveCount(0)
  })
})
