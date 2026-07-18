import { test, expect } from '@playwright/test'

test.describe('Application', () => {
  test('should load the home page', async ({ page }) => {
    await page.goto('/')
    
    await expect(page.locator('h1')).toContainText('铸己匣 - OpenForgeSelf')
  })

  test('should display development message', async ({ page }) => {
    await page.goto('/')
    
    await expect(page.locator('p')).toContainText('聊天界面开发中...')
  })

  test('should have correct page title', async ({ page }) => {
    await page.goto('/')
    
    await expect(page).toHaveTitle(/OpenForgeSelf/)
  })
})