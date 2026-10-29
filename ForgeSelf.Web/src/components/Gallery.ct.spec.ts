/**
 * 画廊壳自身的契约（导航 / 深链 / 错误页）
 *
 * 这是「调试框架本身」的用例：不测产品组件，只测 ct/main.ts 的挂载契约。
 * 因此本文件没有同名的 `.story.ts`（守卫对此有显式豁免）。
 */
import { test, expect } from '@playwright/test'

test.describe('画廊壳 · 导航与深链', () => {
  test('侧栏列出全部已登记故事，点击即切换组件', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await page.goto('/')

    const nav = page.locator('#gallery-nav')
    await expect(nav).toBeVisible()
    // 分组标题来自 ct/stories.ts 的 navConfig（配置生效的机械证据）
    await expect(nav.locator('.nav-group-title').first()).toContainText('ForgeCard · 玻璃卡片')

    // 四个组件族都必须出现（新增故事时不必改这里，断言的是「注册即出现」的契约）
    for (const id of ['ForgeCard/Default', 'PageHead/TitleOnly', 'EmptyState/Full', 'AIAgent/MessageText']) {
      await expect(nav.locator(`li[data-story-id="${id}"]`)).toHaveCount(1)
    }

    // 默认选中第一个故事并渲染
    await expect(page.locator('.gal-preview .forge-card')).toHaveCount(1)

    // 点击跨组故事：挂载区换成聊天消息，选中态跟随
    await nav.locator('li[data-story-id="AIAgent/MessageText"]').click()
    await expect(page.locator('.gal-preview .aiw-msg')).toHaveCount(1)
    await expect(page.locator('.gal-preview .forge-card')).toHaveCount(0)
    await expect(nav.locator('li[data-story-id="AIAgent/MessageText"]')).toHaveClass(/active/)
  })

  test('深链模式保留侧栏并高亮当前故事（人类浏览）', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await page.goto('/?story=ForgeCard/Default')
    await page.waitForSelector('.forge-card')

    const nav = page.locator('#gallery-nav')
    await expect(nav).toBeVisible()
    await expect(nav.locator('.nav-group').first()).toBeVisible()
    await expect(nav.locator('li[data-story-id="ForgeCard/Default"]')).toHaveClass(/active/)
    await expect(page.locator('.gal-preview .forge-card')).toHaveCount(1)
  })

  test('chrome=0 不渲染侧栏（CT 截图基线不被导航干扰）', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await page.goto('/?story=ForgeCard/Default&chrome=0')
    await page.waitForSelector('.forge-card')

    await expect(page.locator('#gallery-nav .nav-group')).toHaveCount(0)
    await expect(page.locator('body')).not.toHaveClass(/has-nav/)
    await expect(page.locator('.gal-preview .forge-card')).toHaveCount(1)
  })

  test('未知故事深链：显示 Unknown story 与可用列表（不白屏）', async ({ page }) => {
    await page.goto('/?story=NotExist/Nope')

    await expect(page.locator('body')).toContainText('Unknown story: NotExist/Nope')
    // 可用 id 列表是排障入口，必须真的列出来
    await expect(page.locator('body')).toContainText('ForgeCard/Default')
  })
})
