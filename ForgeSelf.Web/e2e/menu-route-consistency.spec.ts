import type { Page } from '@playwright/test'
import { test, expect } from './fixtures/e2e'
import { getRealApiKey } from './helpers/real-auth'

/**
 * 批次A（菜单/路由真源一致性）防漂移 e2e —— 走 e2e-testing 技能统一 globalSetup 全新宿主，零 mock。
 *
 * 背景（seq92 分析）：插件界面入口存在两套并行登记处——
 *   链路A：plugin.json frontend → GET /api/plugin/frontend-manifest（宿主注册路由的真源）；
 *   链路B：后端 IMenuExtension → GET /api/plugin/menu-items（与 A 漂移即产生悬空菜单，如 D1 /quicklinks）。
 * 本 spec 用运行时真实接口 + 真实浏览器路由做四组对账断言（铁律 19③：route 改名/增删必须同步本文件）：
 *   ① 双源逐条对账（AC-5）：每个「已启用且声明 frontend.menu+route」插件在 menu-items 恰有一条 Path 一致项（不双发）；
 *   ② 真实导航（任务书 T5②）：manifest 每个「启用 + 有 route + views[0]」项导航该路径，断言真实渲染非空渲染/非错误态；
 *   ③ 可导航性（任务书 T5③）：menu-items 每条**顶层** Path 必须能被运行时 vue-router 真实解析
 *      （live router = 静态路由 ∪ manifest 注册路由 ∪ /plugin 命名空间，是本集合运算的唯一真源；
 *       子菜单项（children）的 /plugin/ 命名空间路由由 Sidebar 挂载时才注册，Sidebar 挂载在批次A 明确范围外，故不入断言）；
 *   ④ 回归断言（AC-1/2/3/4）：全表无 Path=/quicklinks；无 pluginId=scheduler/sample 项（决策①A 撤销）；
 *      mcp-center、design-system 在列且 Name/Path/Icon 与 manifest 逐字段相等（D2 补发）。
 *
 * 运行：pnpm exec playwright test --config=playwright.config.ts e2e/menu-route-consistency.spec.ts
 */

interface MenuChild {
  id: string
  name: string
  icon?: string
  path: string
  pluginId: string
}

interface MenuItem {
  id: string
  name: string
  icon?: string
  path: string
  order?: number
  pluginId: string
  children?: MenuChild[] | null
}

interface ManifestFrontend {
  views?: string[] | null
  menu?: string | null
  route?: string | null
  icon?: string | null
  entry?: string | null
}

interface ManifestItem {
  id: string
  name: string
  isEnabled: boolean
  frontend?: ManifestFrontend | null
}

async function fetchApi<T>(page: Page, url: string): Promise<T> {
  const resp = await page.request.get(url, {
    headers: { Authorization: `Bearer ${getRealApiKey()}` },
  })
  expect(resp.status(), `${url} 应返回 200`).toBe(200)
  const body = (await resp.json()) as { success: boolean; data: T }
  expect(body.success, `${url} 响应壳 success 应为 true`).toBe(true)
  return body.data
}

/** 冷启动远程界面偶发加载失败时按页面「重试」兜底（与 home.spec gotoHome 同款容忍）。 */
async function gotoAndExpectRender(page: Page, routePath: string): Promise<void> {
  await page.goto(routePath)
  for (let attempt = 0; attempt < 3; attempt++) {
    const errState = page.locator('.plugin-view-state--error')
    if (await errState.isVisible().catch(() => false)) {
      const retry = page.locator('.plugin-view-state--error button, .retry-btn')
      if (await retry.count().catch(() => 0)) {
        await retry.first().click()
        await page.waitForTimeout(800)
        continue
      }
    }
    break
  }
  // 真实渲染 = 路由出口 .main-content 出现至少一个子元素（无 catch-all 路由时未匹配路由渲染为空）
  await expect
    .poll(
      async () =>
        page.evaluate(() => {
          const main = document.querySelector('.main-content')
          return main ? main.childElementCount : 0
        }),
      `${routePath} 应真实渲染出路由内容`,
    )
    .toBeGreaterThan(0)
  await expect(
    page.locator('.plugin-view-state--error'),
    `${routePath} 不应停留在插件视图错误态`,
  ).toHaveCount(0)
}

test.describe('菜单声明 ↔ 实际路由 一致性对账（批次A 防漂移门禁）', () => {
  test('① 双源逐条对账：manifest 界面声明在 menu-items 恰有一条一致项（diff=0，不双发）', async ({
    page,
  }) => {
    await page.goto('/home')
    const [menuItems, manifest] = await Promise.all([
      fetchApi<MenuItem[]>(page, '/api/plugin/menu-items'),
      fetchApi<ManifestItem[]>(page, '/api/plugin/frontend-manifest'),
    ])

    const expected = manifest.filter(
      (m) => m.isEnabled && m.frontend?.menu?.trim() && m.frontend?.route?.trim(),
    )
    expect(expected.length, 'e2e 环境应存在 manifest 界面声明插件（防止静默空集）').toBeGreaterThan(0)

    const diff: string[] = []
    for (const m of expected) {
      const items = menuItems.filter((i) => i.pluginId === m.id)
      if (items.length !== 1) {
        diff.push(`${m.id}: menu-items 出现 ${items.length} 条（应恰 1 条，防双发/缺发）`)
        continue
      }
      if (items[0].path !== m.frontend!.route) {
        diff.push(`${m.id}: menu Path=${items[0].path} ≠ manifest route=${m.frontend!.route}`)
      }
    }
    expect(diff, `双源对账差异:\n${diff.join('\n')}`).toHaveLength(0)
  })

  test('② manifest「启用+route+views」项逐一真实导航，断言真实渲染非兜底空页', async ({ page }) => {
    await page.goto('/home')
    const manifest = await fetchApi<ManifestItem[]>(page, '/api/plugin/frontend-manifest')
    const navigable = manifest.filter(
      (m) => m.isEnabled && m.frontend?.route?.trim() && (m.frontend.views?.length ?? 0) > 0,
    )
    expect(navigable.length, 'e2e 环境应存在可导航 manifest 界面插件').toBeGreaterThan(0)

    for (const m of navigable) {
      await gotoAndExpectRender(page, m.frontend!.route!)
      expect(page.url(), `${m.id} 导航后应停留在声明路由`).toContain(m.frontend!.route!)
    }
  })

  test('③ menu-items 每条顶层 Path 必须被运行时路由表真实解析（悬空声明即 Fail）', async ({
    page,
  }) => {
    await page.goto('/home')
    const menuItems = await fetchApi<MenuItem[]>(page, '/api/plugin/menu-items')
    expect(menuItems.length, 'menu-items 不应为空').toBeGreaterThan(0)

    // 运行时 vue-router 是「注册路由」唯一真源：静态表 ∪ manifest 动态注册 ∪（Sidebar 挂载时的）/plugin 命名空间
    const resolvePath = (p: string) =>
      page.evaluate((target) => {
        const app = (document.getElementById('app') as HTMLElement & { __vue_app__?: any })
          ?.__vue_app__
        const router = app?.config?.globalProperties?.$router
        if (!router) return false
        return router.resolve(target).matched.length > 0
      }, p)

    for (const item of menuItems) {
      expect(
        await resolvePath(item.path),
        `menu-items 顶层项 ${item.id}(plugin=${item.pluginId}) Path=${item.path} 无法被运行时路由解析（悬空菜单）`,
      ).toBe(true)
    }
  })

  test('④ 回归断言：/quicklinks 清零；scheduler/sample 已撤销；mcp-center/design-system 补发且逐字段一致', async ({
    page,
  }) => {
    await page.goto('/home')
    const [menuItems, manifest] = await Promise.all([
      fetchApi<MenuItem[]>(page, '/api/plugin/menu-items'),
      fetchApi<ManifestItem[]>(page, '/api/plugin/frontend-manifest'),
    ])

    // AC-1：全表（含子项）无 Path=/quicklinks
    const allPaths = menuItems.flatMap((i) => [i.path, ...(i.children ?? []).map((c) => c.path)])
    expect(allPaths, '菜单中不应再出现悬空 /quicklinks').not.toContain('/quicklinks')

    // AC-2/AC-3（决策①A）：scheduler/sample 悬空菜单声明已撤销
    expect(
      menuItems.filter((i) => i.pluginId === 'scheduler'),
      'scheduler 无界面，悬空菜单声明应已撤销',
    ).toHaveLength(0)
    expect(
      menuItems.filter((i) => i.pluginId === 'sample'),
      'sample 无界面，悬空菜单声明应已撤销',
    ).toHaveLength(0)

    // AC-4：D2 补发的两项存在且与 manifest 逐字段相等（Name/Path/Icon）
    for (const pluginId of ['mcp-center', 'design-system']) {
      const item = menuItems.find((i) => i.pluginId === pluginId)
      expect(item, `menu-items 应含 ${pluginId} 项`).toBeTruthy()
      const manifestItem = manifest.find((m) => m.id === pluginId)
      expect(manifestItem, `frontend-manifest 应含 ${pluginId}`).toBeTruthy()
      expect(item!.name, `${pluginId} 菜单名应与 frontend.menu 一致`).toBe(manifestItem!.frontend!.menu)
      expect(item!.path, `${pluginId} Path 应与 frontend.route 一致`).toBe(
        manifestItem!.frontend!.route,
      )
      expect(item!.icon ?? '', `${pluginId} Icon 应与 frontend.icon 一致`).toBe(
        manifestItem!.frontend!.icon ?? '',
      )
    }
  })
})
