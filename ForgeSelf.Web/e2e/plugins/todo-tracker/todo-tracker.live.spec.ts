import { test, expect, type Page } from '@playwright/test'
import { copyFileSync, mkdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'
import { backendUrl, frontendUrl } from '../../helpers/e2e-env'

/**
 * 起源码（dev-stack）走查：TodoTracker 抽屉渐进披露 AC-UI-1…6。
 * 运行：E2E_API_TOKEN + E2E_FRONTEND_URL + E2E_BACKEND_URL
 *   pnpm exec playwright test --config=playwright.live.config.ts e2e/plugins/todo-tracker/todo-tracker.live.spec.ts
 */

const PORT = process.env.E2E_FRONTEND_PORT ?? 'live-dev'
const OUT_DIR = path.resolve(fileURLToPath(new URL(`../../../screenshots/${PORT}`, import.meta.url)))
const ARTIFACTS = '/opt/cursor/artifacts'
const ROUTE = '/todo'

async function shot(page: Page, name: string): Promise<string> {
  mkdirSync(OUT_DIR, { recursive: true })
  mkdirSync(ARTIFACTS, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.png`)
  const art = path.join(ARTIFACTS, `${name}.png`)
  await page.screenshot({ path: file, fullPage: false })
  copyFileSync(file, art)
  console.log(`[shot] ${file} → ${art}`)
  return art
}

test.describe('起源码 dev-stack · todo-tracker 抽屉走查', () => {
  test.describe.configure({ timeout: 180_000 })

  test('AC-UI：创建仅标题、自动开抽屉、全宽列表、渐进披露、缺项反馈、Esc 关闭', async ({ page }) => {
    const token = process.env.E2E_API_TOKEN?.trim()
    expect(token, '缺少 E2E_API_TOKEN').toBeTruthy()

    await injectRealApiKey(page)
    await page.goto(`${frontendUrl()}${ROUTE}`, { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: '待办任务' })).toBeVisible({ timeout: 60_000 })
    await expect(page.locator('.tt-root')).toBeVisible()

    // 列表全宽基线（开抽屉前）
    const listBefore = await page.locator('.tt-list-panel').boundingBox()
    expect(listBefore?.width ?? 0).toBeGreaterThan(600)
    await shot(page, 'tt-ac1-list-fullwidth-before')

    const title = `走查-抽屉-${Date.now()}`
    await page.locator('.tt-create-input').fill(title)
    await page.getByRole('button', { name: '新建' }).click()

    const drawer = page.locator('[data-test="task-drawer"]')
    await expect(drawer).toBeVisible({ timeout: 30_000 })
    await expect(drawer.locator('#td-title')).toHaveValue(title)
    // 概要可见；四栏分类默认收起
    await expect(drawer.locator('[data-test="detail-soft-hint"]')).toContainText('下发前待补齐')
    await expect(drawer.locator('[data-test="section-workunit"] .td-section-head')).toBeVisible()
    await expect(drawer.locator('#td-obj')).toHaveCount(0)
    await expect(drawer.locator('[data-test="section-basic"] .td-section-body')).toHaveCount(0)
    await shot(page, 'tt-ac2-create-drawer-summary-collapsed')

    // 开抽屉后列表不被分栏挤压（主区仍全宽，抽屉为 overlay）
    const listAfter = await page.locator('.tt-list-panel').boundingBox()
    expect(Math.abs((listAfter?.width ?? 0) - (listBefore?.width ?? 0))).toBeLessThan(40)
    await shot(page, 'tt-ac3-list-width-with-drawer')

    // 列表软提示非危险「还缺」
    const row = page.locator('.tt-item', { hasText: title })
    await expect(row.locator('[data-test="list-soft-hint"]')).toContainText('下发前待补齐')
    await expect(row.locator('[data-test="list-soft-hint"]')).not.toHaveCSS('color', 'rgb(245, 108, 108)')

    // 点去补齐 → 展开工作单元
    await drawer.locator('[data-test="go-fill-workunit"]').click()
    await expect(drawer.locator('#td-obj')).toBeVisible()
    await shot(page, 'tt-ac4-expand-workunit-from-hint')

    // 分类渐进：折叠/展开
    await drawer.locator('[data-test="section-basic"] .td-section-head').click()
    await expect(drawer.locator('#td-remark')).toBeVisible()
    await drawer.locator('[data-test="section-project"] .td-section-head').click()
    await expect(drawer.locator('[data-test="section-project"] .td-section-body')).toBeVisible()
    await drawer.locator('[data-test="section-scope"] .td-section-head').click()
    await expect(drawer.locator('#td-allow')).toBeVisible()
    await drawer.locator('[data-test="section-dispatch"] .td-section-head').click()
    await expect(drawer.locator('[data-test="dispatch-btn"]')).toBeVisible()
    await drawer.locator('[data-test="section-history"] .td-section-head').click()
    await expect(drawer.locator('[data-test="execution-timeline"]')).toBeVisible()
    await shot(page, 'tt-ac5-sections-expanded')

    // 缺项点下发 → 点名反馈
    await drawer.locator('[data-test="dispatch-btn"]').click()
    await expect(drawer.locator('[data-test="workunit-missing"]')).toContainText('下发前还缺')
    await shot(page, 'tt-ac5b-dispatch-missing-feedback')

    // Escape 关抽屉
    await page.keyboard.press('Escape')
    await expect(drawer).toHaveCount(0)
    await shot(page, 'tt-ac6-escape-closed')

    // 看板同样开抽屉（断言落在视口内，防屏外假绿）
    await page.locator('.tt-view-switch').getByRole('button', { name: '看板' }).click()
    await expect(page.locator('.tt-board-card', { hasText: title })).toBeVisible({ timeout: 30_000 })
    await page.locator('.tt-board-card', { hasText: title }).click()
    const boardDrawer = page.locator('[data-test="task-drawer"]')
    await expect(boardDrawer).toBeVisible()
    const drawerBox = await boardDrawer.boundingBox()
    expect(drawerBox, '看板开抽屉后应有几何框').toBeTruthy()
    expect(drawerBox!.x + drawerBox!.width / 2, '抽屉中心应在视口内').toBeLessThan(page.viewportSize()!.width)
    await expect(boardDrawer.locator('#td-title')).toHaveValue(title)
    await shot(page, 'tt-ac7-board-opens-drawer')

    // 失焦保存：改备注 → blur → 关开抽屉 → 值仍在
    await boardDrawer.locator('[data-test="section-basic"] .td-section-head').click()
    const remark = `失焦保存-${Date.now()}`
    await boardDrawer.locator('#td-remark').fill(remark)
    await boardDrawer.locator('#td-remark').blur()
    await page.waitForTimeout(400)
    await shot(page, 'tt-ac8-blur-save-before-reopen')
    await page.locator('[data-test="drawer-close"]').click()
    await expect(page.locator('[data-test="task-drawer"]')).toHaveCount(0)
    await page.locator('.tt-board-card', { hasText: title }).click()
    const reopened = page.locator('[data-test="task-drawer"]')
    await expect(reopened).toBeVisible()
    await reopened.locator('[data-test="section-basic"] .td-section-head').click()
    await expect(reopened.locator('#td-remark')).toHaveValue(remark)
    await shot(page, 'tt-ac8-blur-save-persisted')

    // 遮罩关闭
    await page.locator('[data-test="task-drawer-mask"]').click({ position: { x: 8, y: 8 } })
    await expect(page.locator('[data-test="task-drawer"]')).toHaveCount(0)
    await shot(page, 'tt-ac9-mask-closed')

    // 窄屏 100%：再开抽屉，宽度应≈视口
    await page.setViewportSize({ width: 390, height: 844 })
    await page.locator('.tt-view-switch').getByRole('button', { name: '列表' }).click()
    await page.locator('.tt-item', { hasText: title }).click()
    const narrowDrawer = page.locator('[data-test="task-drawer"]')
    await expect(narrowDrawer).toBeVisible()
    const narrowBox = await narrowDrawer.boundingBox()
    expect(narrowBox?.width ?? 0).toBeGreaterThan(360)
    expect(Math.abs((narrowBox?.width ?? 0) - 390)).toBeLessThan(24)
    await shot(page, 'tt-ac10-narrow-fullwidth-drawer')

    // 清理：关抽屉后用 API 删任务（不点界面删除，避免误伤）
    await page.locator('[data-test="drawer-close"]').click()
    const idMatch = await page.evaluate(async ({ api, auth, t }) => {
      const res = await fetch(`${api}/api/todos?q=${encodeURIComponent(t)}&page=1&pageSize=5`, {
        headers: { Authorization: `Bearer ${auth}` },
      })
      const json = await res.json() as { data?: { items?: { id: number; title: string }[] } }
      return json.data?.items?.find(i => i.title === t)?.id ?? 0
    }, { api: backendUrl(), auth: token!, t: title })
    if (idMatch) {
      await fetch(`${backendUrl()}/api/todos/${idMatch}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
      })
    }
  })
})
