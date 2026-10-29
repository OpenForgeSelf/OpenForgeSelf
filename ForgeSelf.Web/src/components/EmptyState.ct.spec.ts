/**
 * EmptyState 组件视觉回归（CT）
 *
 * 覆盖两个分支：完整空态（图标 + 主/副文案 + 引导）与仅主文案。
 * 「可选块该消失时真的消失」是空态组件最常见的回归点，故每条都断言 DOM 计数。
 *
 * 运行：pnpm run test:ct        更新基线：pnpm run test:ct:update
 */
import { test, expect } from '@playwright/test'
import { openStory } from './ct-support'

const box = (page: import('@playwright/test').Page) => page.locator('.gal-preview .empty-state')

test.describe('EmptyState · 视觉与契约', () => {
  test('完整空态：图标 + 主文案 + 副文案 + 引导按钮', async ({ page }) => {
    await openStory(page, 'EmptyState/Full', '.empty-state')

    await expect(box(page).locator('.empty-state__icon')).toHaveCount(1)
    await expect(box(page).locator('.empty-state__title')).toHaveText('还没有任何器物')
    await expect(box(page).locator('.empty-state__sub')).toContainText('EMPTY')
    await expect(box(page).getByRole('button', { name: '去创建' })).toBeVisible()

    await expect(box(page)).toHaveScreenshot()
  })

  test('仅主文案：图标/副文案/引导全部不渲染', async ({ page }) => {
    await openStory(page, 'EmptyState/TitleOnly', '.empty-state')

    await expect(box(page).locator('.empty-state__title')).toHaveText('暂无数据')
    await expect(box(page).locator('.empty-state__icon')).toHaveCount(0)
    await expect(box(page).locator('.empty-state__sub')).toHaveCount(0)
    await expect(box(page).locator('.empty-state__action')).toHaveCount(0)

    await expect(box(page)).toHaveScreenshot()
  })
})
