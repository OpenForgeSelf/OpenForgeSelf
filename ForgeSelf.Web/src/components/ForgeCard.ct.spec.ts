/**
 * ForgeCard 组件视觉回归（CT）
 *
 * 走共享画廊：`?story=<id>` 深链挂载变体，先行为断言、再 `toHaveScreenshot()` 锁像素。
 * 只看截图不断言的用例会把错误状态固化成基线，因此每个变体都钉一句「用户能做成什么」。
 *
 * 运行：pnpm run test:ct        更新基线：pnpm run test:ct:update
 */
import { test, expect } from '@playwright/test'
import { openStory } from './ct-support'

const card = (page: import('@playwright/test').Page) => page.locator('.gal-preview .forge-card')

test.describe('ForgeCard · 视觉与契约', () => {
  test('默认态：玻璃卡渲染正文、默认内边距 16px、不可交互', async ({ page }) => {
    await openStory(page, 'ForgeCard/Default', '.forge-card')

    await expect(card(page)).toHaveCount(1)
    await expect(card(page)).toHaveClass(/glass/)
    await expect(card(page)).not.toHaveClass(/forge-card--interactive/)
    await expect(card(page)).toContainText('器物玻璃卡片')
    // padding 默认值 16px（设计稿 glass 卡刻度），必须落到内联样式而不是被其它规则覆盖
    await expect(card(page)).toHaveCSS('padding', '16px')

    await expect(card(page)).toHaveScreenshot()
  })

  test('可交互态：interactive 类、指针与 tabindex 到齐，hover 出现金边', async ({ page }) => {
    await openStory(page, 'ForgeCard/Interactive', '.forge-card')

    await expect(card(page)).toHaveClass(/forge-card--interactive/)
    await expect(card(page)).toHaveCSS('cursor', 'pointer')
    // 可交互卡片必须可用键盘到达（Enter 触发 onClick 的前提）
    await expect(card(page)).toHaveAttribute('tabindex', '0')

    await card(page).hover()
    // 过渡 220ms（--dur-base）落地后再截图，否则截到过渡中的阴影
    await page.waitForTimeout(260)
    await expect(card(page)).toHaveScreenshot()
  })

  test('padding prop：20px 真实生效', async ({ page }) => {
    await openStory(page, 'ForgeCard/Padding20', '.forge-card')

    await expect(card(page)).toHaveCSS('padding', '20px')

    await expect(card(page)).toHaveScreenshot()
  })
})
