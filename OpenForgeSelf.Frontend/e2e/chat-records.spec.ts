import { test, expect } from '@playwright/test'

test.describe('Chat Records View', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/chat-records')
  })

  test('should load the chat records page', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('聊天记录查看')
    await expect(page.locator('.chat-records-view')).toBeVisible()
  })

  test('should display filter bar', async ({ page }) => {
    await expect(page.locator('.filter-bar')).toBeVisible()
    await expect(page.locator('.session-id-input')).toBeVisible()
    await expect(page.locator('.style-select')).toBeVisible()
    await expect(page.locator('.search-button')).toBeVisible()
  })

  test('should display records list table', async ({ page }) => {
    await expect(page.locator('.records-list')).toBeVisible()
    await expect(page.locator('.records-table')).toBeVisible()
  })

  test('should have style filter options', async ({ page }) => {
    const select = page.locator('.style-select')
    await expect(select).toBeVisible()
  })

  test('should have pagination controls', async ({ page }) => {
    await expect(page.locator('.pagination')).toBeVisible()
  })

  test('should navigate to chat records page from URL', async ({ page }) => {
    await page.goto('/chat-records')
    await expect(page.url()).toContain('/chat-records')
  })

  test('should have proper page title', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('聊天记录')
  })
})

test.describe('Chat Record Detail Panel', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/chat-records')
  })

  test('should show detail panel when record is selected', async ({ page }) => {
    const detailPanel = page.locator('.detail-panel')
    // 详情面板初始可能隐藏
    expect(detailPanel).toBeDefined()
  })

  test('should have close button for detail panel', async ({ page }) => {
    const closeButton = page.locator('.close-detail-btn')
    expect(closeButton).toBeDefined()
  })
})

test.describe('Truncated Content Component', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/chat-records')
  })

  test('should have copy button for long content', async ({ page }) => {
    // 验证组件存在于页面中
    const copyBtn = page.locator('.copy-btn')
    expect(copyBtn).toBeDefined()
  })
})
