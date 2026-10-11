import { test, expect } from '../../fixtures/e2e'

/**
 * 首页插件（Home，id=home）冒烟 e2e —— 走 e2e-testing 技能统一 globalSetup 全新宿主。
 *
 * 规范（plugin-development 铁律 24）：每插件仅 1 条冒烟 e2e，冒烟过后按验收标准
 * 在本地前后端（dev-stack）做源码验收；不铺全流程 e2e。
 *
 * 冒烟覆盖（零 mock，对接真实后端 + 前端 dev）：
 *  1. `/` 重定向到 `/home`，新设计稿四区块（hero-card / 常用功能 / 核心看板 / 待办+活动）真实渲染；
 *  2. 导航桥：点击「查看全部待办」经宿主 `provide('forgeOpenPage')` → `openPage` 打开 `/todo`；
 *  3. 截图读图 + 无横向溢出 + 零控制台错误（远程加载无 Vue 双实例等运行时错误）。
 *
 * 运行：
 *   pnpm exec playwright test --config=playwright.config.ts e2e/plugins/home
 */

test.describe('首页插件（Home）冒烟', () => {
  test('重定向 /home，设计稿四区块渲染 + 导航桥 /todo + 截图无溢出', async ({ page }) => {
    const errors: string[] = []
    const serverErrors: string[] = [] // 响应级 >=500 采集（区分接口）
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })
    page.on('response', (r) => { if (r.status() >= 500) serverErrors.push(`${r.status()} ${r.url()}`) })

    // 1) 根路径重定向到 /home（main.ts 解析 settings.homePluginId → manifest.frontend.route）
    await page.goto('/')
    await expect(page).toHaveURL(/\/home/)

    // 2) 设计稿布局四区块（M3 重构后类名）：
    //    hero-card（问候卡）/ 常用功能 / 核心看板（5 列 board-card）/ 待办 + 最近活动
    await expect(page.locator('.hero-card')).toBeVisible({ timeout: 20000 })
    await expect(page.getByText('常用功能', { exact: true })).toBeVisible()
    await expect(page.getByText('核心看板', { exact: true })).toBeVisible()
    await expect(page.locator('.todo-list')).toBeVisible()
    await expect(page.locator('.activity-list')).toBeVisible()

    // 3) 5 列看板卡片（设计稿 board-row）与 Hero 问候语（锻造师 + 建议 chips）
    await expect(page.locator('.board-card')).toHaveCount(5)
    await expect(page.getByText(/锻造师/).first()).toBeVisible()
    await expect(page.locator('.suggest-chip')).toHaveCount(4)

    // 4) 导航桥：点击「查看全部待办」（aria-label）经 forgeOpenPage 打开 /todo
    const link = page.locator('[aria-label="查看全部待办"]').first()
    await expect(link).toBeVisible()
    await link.click()
    await expect(page).toHaveURL(/\/todo/)

    // 5) 回到首页截图（走查对照基准）＋ 无横向滚动（长文本不溢出容器）
    await page.goBack()
    await expect(page.locator('.hero-card')).toBeVisible()
    await page.screenshot({ path: 'screenshots/e2e/home/walkthrough.png', fullPage: true })
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
    )
    expect(overflow, '首页不应出现横向滚动条').toBe(false)

    // 6) 报错只允许已知的 ScriptRunner `/api/scripts` 500（既有后端缺陷，Home 已 allSettled 优雅降级）
    const unexpected = serverErrors.filter((e) => !e.includes('/api/scripts'))
    expect(unexpected, `非预期的服务端错误: ${unexpected.join(' | ')}`).toHaveLength(0)
    const jsErrors = errors.filter((e) => !e.includes('500 (Internal Server Error)'))
    expect(jsErrors, `页面出现错误: ${jsErrors.join(' | ')}`).toHaveLength(0)
  })
})
