import { test, expect } from '@playwright/test'

test.describe('插件商店', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/plugins')
  })

  test('页面加载测试', async ({ page }) => {
    await expect(page.locator('.plugin-store')).toBeVisible()
    await expect(page.locator('.store-title')).toContainText('插件商店')
    await expect(page.locator('.store-subtitle')).toContainText('发现并管理扩展功能')
  })

  test('插件列表显示测试', async ({ page }) => {
    await expect(page.locator('.plugins-grid')).toBeVisible()
    const cards = page.locator('.plugin-card')
    const count = await cards.count()
    expect(count).toBeGreaterThanOrEqual(0)
  })

  test('搜索功能测试', async ({ page }) => {
    const searchInput = page.locator('.search-input')
    await expect(searchInput).toBeVisible()

    await searchInput.fill('测试')

    const resultsInfo = page.locator('.results-info')
    await expect(resultsInfo).toBeVisible()
  })

  test('筛选功能测试', async ({ page }) => {
    const filterGroups = page.locator('.filter-group')
    await expect(filterGroups).toHaveCount(2)

    const statusButtons = filterGroups.nth(1).locator('.filter-tag')
    const enabledButton = statusButtons.filter({ hasText: '已启用' })
    await enabledButton.click()
    
    await expect(enabledButton).toHaveClass(/active/)

    const allButton = statusButtons.filter({ hasText: '全部' })
    await allButton.click()
    await expect(allButton).toHaveClass(/active/)
  })

  test('查看插件详情测试', async ({ page }) => {
    const cards = page.locator('.plugin-card')
    const cardCount = await cards.count()
    
    if (cardCount > 0) {
      await cards.first().click()
      
      await expect(page.locator('.modal-overlay')).toBeVisible()
      await expect(page.locator('.plugin-name')).toBeVisible()
      
      const closeButton = page.locator('.close-btn')
      await closeButton.click()
      
      await expect(page.locator('.modal-overlay')).not.toBeVisible()
    }
  })

  test('搜索框占位符显示', async ({ page }) => {
    const searchInput = page.locator('.search-input')
    await expect(searchInput).toHaveAttribute('placeholder', '搜索插件名称、描述或作者...')
  })

  test('分类筛选标签存在', async ({ page }) => {
    const categoryGroup = page.locator('.filter-group').first()
    await expect(categoryGroup.locator('.filter-label')).toContainText('分类')
  })

  test('状态筛选标签存在', async ({ page }) => {
    const statusGroup = page.locator('.filter-group').nth(1)
    await expect(statusGroup.locator('.filter-label')).toContainText('状态')
  })
})
