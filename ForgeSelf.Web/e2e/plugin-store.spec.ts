import { test, expect } from '@playwright/test'
import { injectRealApiKey } from './helpers/real-auth'

/**
 * 插件管理页（PluginStore）走查 spec —— 「发布后走查插件页」的正规入口。
 *
 * 覆盖（对齐 PluginStore.vue 当前 DOM，2026-09-24 改造后结构）：
 *   - 页面加载 + 卡片渲染（带真实 token 请求 /api/plugin 成功，非空态）
 *   - 版本徽标（035「发布带版本号」：每卡片 footer-left 显 v版本号）
 *   - 启用/停用标签（035「显示启用插件」：.enabled-badge on/off 与文案一致）
 *   - 搜索过滤 + 结果计数
 *   - 截图存档（视觉证据）
 *
 * 认证: 由 globalSetup 构建宿主 + 起实例 + 解密真实 token（real-auth.ts）注入
 *       localStorage，零手工。运行态宿主（51888）手工走查请用
 *       scripts/get-forge-token.cjs 拿 token 注入 —— 不要每次现写探针。
 */

test.describe('插件管理页走查（带真实 token）', () => {
  test.beforeEach(async ({ page }) => {
    await injectRealApiKey(page)
    await page.goto('/plugins')
  })

  test('页面加载与插件卡片渲染（token 鉴权通过，非空态）', async ({ page }) => {
    await expect(page.locator('.plugin-store-page')).toBeVisible()
    await expect(page.locator('.plugins-grid')).toBeVisible()

    const cards = page.locator('.plugin-card')
    const count = await cards.count()
    expect(count).toBeGreaterThan(0)
    // 若 401/鉴权失败，页面会走空态分支（.empty-state）而不是渲染卡片 —— 此断言即鉴权回归守卫
    await expect(page.locator('.empty-state')).toHaveCount(0)
  })

  test('每张卡片显示 v版本号（035 发布带版本号）', async ({ page }) => {
    const cards = page.locator('.plugin-card')
    // count() 不做自动等待：先等首卡出现（= /api/plugin 已返回并渲染），再统计
    await expect(cards.first()).toBeVisible()
    const count = await cards.count()
    expect(count).toBeGreaterThan(0)

    for (let i = 0; i < count; i++) {
      const versionText = await cards.nth(i).locator('.plugin-version').textContent()
      expect(versionText?.trim()).toMatch(/^v\d+\.\d+\.\d+$/)
    }
  })

  test('启用/停用标签（035 显示启用插件）文案与状态类一致', async ({ page }) => {
    const cards = page.locator('.plugin-card')
    await expect(cards.first()).toBeVisible()
    const count = await cards.count()
    expect(count).toBeGreaterThan(0)

    const enabled = cards.locator('.enabled-badge.on')
    const disabled = cards.locator('.enabled-badge.off')
    const total = (await enabled.count()) + (await disabled.count())
    expect(total).toBe(count)

    for (const badge of await cards.locator('.enabled-badge').all()) {
      const text = (await badge.textContent())?.trim()
      const cls = await badge.getAttribute('class')
      if (text === '已启用') expect(cls).toContain('on')
      else if (text === '已停用') expect(cls).toContain('off')
      else throw new Error(`未知启用状态文案: ${text}`)
    }
  })

  test('搜索过滤与结果计数', async ({ page }) => {
    const searchInput = page.locator('.search-input')
    await expect(searchInput).toBeVisible()

    await searchInput.fill('插件')
    const resultsInfo = page.locator('.results-info')
    await expect(resultsInfo).toBeVisible()
    const text = (await resultsInfo.textContent()) ?? ''
    expect(text).toMatch(/个插件|共 \d+|匹配/)

    // 空搜索结果 → 空态
    await searchInput.fill('__绝对不存在的关键字__')
    await expect(page.locator('.empty-state')).toBeVisible()
  })

  test('走查截图存档（视觉证据）', async ({ page }) => {
    const cards = page.locator('.plugin-card')
    await expect(cards.first()).toBeVisible()
    await page.screenshot({
      path: 'screenshots/e2e/plugin-store/plugin-store-walkthrough.png',
      fullPage: true,
    })
  })

  test('版本按钮可打开版本历史弹窗（035 显式版本操作 UI）', async ({ page }) => {
    const cards = page.locator('.plugin-card')
    await expect(cards.first()).toBeVisible()

    // 每个卡片有「版本」按钮；点击打开版本历史弹窗
    const versionBtn = cards.first().locator('.version-btn')
    await expect(versionBtn).toBeVisible()
    await versionBtn.click()

    const dialog = page.locator('.el-dialog')
    await expect(dialog).toBeVisible()
    await expect(dialog.locator('.el-dialog__title')).toHaveText('版本历史')

    // 回归守卫（2026-09-24）：打开版本历史不得刷新整个列表 —— 卡片网格必须保持可见
    await expect(page.locator('.plugins-grid')).toBeVisible()

    // 隔离宿主插件为扁平布局（未跑迁移脚本）：弹窗应渲染空态说明而非崩溃
    const body = dialog.locator('.versions-body')
    await expect(body).toBeVisible()
  })

  test('插件卡片提供「打开页面」入口（直达插件自身页面）', async ({ page }) => {
    await page.goto('/plugins')
    const cards = page.locator('.plugin-card')
    await expect(cards.first()).toBeVisible({ timeout: 10000 })

    // 已启用且声明了前端页面的插件卡片应有「打开页面」按钮
    const card = cards.filter({ hasText: 'AI Agent' }).first()
    await expect(card).toBeVisible({ timeout: 10000 })
    const openPageBtn = card.getByRole('button', { name: /打开页面/ })
    await expect(openPageBtn).toBeVisible({ timeout: 10000 })
    await expect(openPageBtn).toBeEnabled()

    // 点击后离开 /plugins 直达插件自身页面
    await openPageBtn.click()
    await page.waitForURL((u) => u.pathname !== '/plugins', { timeout: 10000 })
    expect(new URL(page.url()).pathname).not.toBe('/plugins')
  })
})
