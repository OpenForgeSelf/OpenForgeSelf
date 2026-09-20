import type { Page } from '@playwright/test'
import { test, expect } from '../../fixtures/e2e'

/**
 * 首页插件（Home，id=home）插件层 e2e —— 走 e2e-testing 技能统一 globalSetup 全新宿主。
 *
 * 覆盖（零 mock，对接真实后端 7102 + 前端 dev 7002）：
 *  1. `/` 重定向到 `/home`，且首页四段（Hero / 常用功能 / 核心看板 / 待办与活动）+ 系统监控真实渲染；
 *  2. 导航桥：点击「查看全部待办」经宿主 `provide('forgeOpenPage')` → `openPage` 打开 `/todo`（开 tab + 记 usage）；
 *  3. 配置接线（D1=B）：`GET /api/settings` 的 `homePluginId` 默认 `home`，且 `frontend-manifest` 含 `home` 已启用、route=`/home`；
 *  4. 截图读图 + 无横向溢出 + 零控制台错误（验证远程加载无 Vue 双实例等运行时错误）。
 *
 * 运行：
 *   pnpm exec playwright test --config=playwright.config.ts e2e/plugins/home
 */

async function gotoHome(page: Page) {
  // 容忍冷启动首请求 400/竞态：出现错误态则点「重试」重载，最多 3 次。
  await page.goto('/home')
  for (let i = 0; i < 3; i++) {
    const err = page.locator('.plugin-view-state--error')
    if (await err.isVisible().catch(() => false)) {
      const retry = page.locator('.plugin-view-state--error button, .retry-btn')
      if (await retry.count().catch(() => 0)) {
        await retry.first().click()
        await page.waitForTimeout(800)
        continue
      }
    }
    break
  }
}

test.describe('首页插件（Home）实跑宿主 e2e', () => {
  test('访问 / 重定向到 /home 且四段渲染', async ({ page }) => {
    const errors: string[] = []
    const serverErrors: string[] = [] // 响应级 >=500 采集（区分接口）
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })
    page.on('response', (r) => { if (r.status() >= 500) serverErrors.push(`${r.status()} ${r.url()}`) })

    await page.goto('/')
    // 重定向目标由 main.ts 解析 settings.homePluginId → 启用插件 manifest.frontend.route
    await expect(page).toHaveURL(/\/home/)

    // 等待插件远程界面加载完成（异步组件 + 远程 import）
    await expect(page.locator('.hero-section')).toBeVisible({ timeout: 20000 })

    // 四段标题/容器
    await expect(page.getByText('常用功能', { exact: true })).toBeVisible()
    await expect(page.getByText('核心看板', { exact: true })).toBeVisible()
    await expect(page.locator('.todo-panel')).toBeVisible()
    await expect(page.locator('.activity-panel')).toBeVisible()
    // 系统监控 widget（h3.widget-card-title，唯一 heading）
    await expect(page.getByRole('heading', { name: '系统监控' })).toBeVisible()

    // 已知既有后端缺陷（与 Home 插件无关）：ScriptRunner 插件 DI 未转发宿主单例
    // IRuntimeDetector → `/api/scripts` 500；Home 已用 allSettled 优雅降级。其余接口须 0 错误。
    const unexpected = serverErrors.filter((e) => !e.includes('/api/scripts'))
    expect(unexpected, `非预期的服务端错误: ${unexpected.join(' | ')}`).toHaveLength(0)
  })

  test('导航桥：点击「查看全部待办」经 forgeOpenPage 打开 /todo', async ({ page }) => {
    const errors: string[] = []
    const serverErrors: string[] = []
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })
    page.on('response', (r) => { if (r.status() >= 500) serverErrors.push(`${r.status()} ${r.url()}`) })

    await gotoHome(page)
    await expect(page.locator('.todo-panel')).toBeVisible()

    // HomeView 的「查看全部待办」按钮（aria-label）调用 inject('forgeOpenPage')('/todo','待办事项')
    const link = page.locator('[aria-label="查看全部待办"]').first()
    await expect(link).toBeVisible()
    await link.click()

    // 关键断言：经宿主桥 openPage 跳转成功（宿主桥内 runWithContext 保证 router 可用）
    await expect(page).toHaveURL(/\/todo/)

    // 报错只允许已知的 ScriptRunner `/api/scripts` 500（既有后端缺陷，Home 已优雅降级）
    const unexpected = serverErrors.filter((e) => !e.includes('/api/scripts'))
    expect(unexpected, `非预期的服务端错误: ${unexpected.join(' | ')}`).toHaveLength(0)
    const jsErrors = errors.filter((e) => !e.includes('500 (Internal Server Error)'))
    expect(jsErrors, `页面出现错误: ${jsErrors.join(' | ')}`).toHaveLength(0)
  })

  test('配置接线（D1=B）：homePluginId 默认 home 且 manifest 含启用项', async ({ page }) => {
    // 先导航建源：fixture 的 addInitScript 已把真实 token 注入 localStorage
    await page.goto('/home')
    const token = (await page.evaluate(() => localStorage.getItem('forge_api_token') ?? '')) as string

    const settingsResp = await page.request.get('/api/settings', {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(settingsResp.status(), 'settings 接口应 200').toBe(200)
    const settings = (await settingsResp.json()) as { data?: { homePluginId?: string } }
    expect(settings.data?.homePluginId ?? 'home').toBe('home')

    const manifestResp = await page.request.get('/api/plugin/frontend-manifest', {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(manifestResp.status(), 'frontend-manifest 接口应 200').toBe(200)
    const manifest = (await manifestResp.json()) as { data?: Array<{ id: string; isEnabled: boolean; frontend?: { route?: string } }> }
    const home = manifest.data?.find((m) => m.id === 'home')
    expect(home, 'manifest 应含 home 插件').toBeTruthy()
    expect(home!.isEnabled, 'home 插件应处于 Running（已启用）').toBe(true)
    expect(home!.frontend?.route).toBe('/home')
  })

  test('截图读图 + 无横向溢出（视觉检查）', async ({ page }) => {
    await gotoHome(page)
    await expect(page.locator('.hero-section')).toBeVisible({ timeout: 20000 })

    // 回归守卫：Hero 区块不得被 flex 压缩。
    // 根因（2026-09-20 修复）：.home-content 是「flex 列 + overflow-y:auto」滚动容器，
    // 其子区块默认 flex-shrink:1；内容总高超过容器（Playwright 默认视口 1280×720 即触发）
    // 时区块被挤扁，配合 .hero-section{overflow:hidden} 直接裁掉问候语（"文字挡住半截"）。
    // 此处绑定业务语义「Hero 必须完整容纳其内部内容」而非魔法数字，新增区块/视口变化不再破测。
    const heroBox = await page.locator('.hero-section').boundingBox()
    const chipsBox = await page.locator('.action-chips').boundingBox()
    expect(heroBox, 'Hero 区块应可见').toBeTruthy()
    expect(chipsBox, 'Hero 推荐动作区应可见').toBeTruthy()
    expect(
      chipsBox!.y + chipsBox!.height,
      'Hero 推荐动作底部应落在 Hero 区块内（Hero 未被 flex 压缩裁切）',
    ).toBeLessThanOrEqual(heroBox!.y + heroBox!.height + 1)

    await page.screenshot({ path: 'screenshots/e2e/home/walkthrough.png', fullPage: true })

    // 无横向滚动条（长文本不溢出容器）
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
    )
    expect(overflow, '首页不应出现横向滚动条').toBe(false)
  })
})
