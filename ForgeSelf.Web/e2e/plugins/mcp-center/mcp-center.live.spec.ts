import { test, expect, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { injectRealApiKey } from '../../helpers/real-auth'
import { frontendUrl } from '../../helpers/e2e-env'

/**
 * 起源码（dev-stack）只读走查：mcp-center 插件页主链路。
 *
 * 驱动方式：playwright.live.config.ts（不另起宿主、不起 webServer，直接打已在跑的实例）。
 * 地址取 e2e-env 的 frontendUrl() —— 由 E2E_FRONTEND_URL 环境变量指定（dev-stack 走查时指向
 * scripts/dev-stack.ps1 起的源码前端端口），默认 playwright.live.config.ts 的 baseURL（:51888）。
 * 令牌：E2E_API_TOKEN 传明文，injectRealApiKey 注入 localStorage['forge_api_token']。
 *
 * 只读边界：只读插件页 + 截图，不点任何不可逆动作（删除/清空/覆盖配置）。
 * 截图落 screenshots/live-dev-<port>/，发包前供用户过目（发布流程 步2.5 硬闸门）。
 *
 * 选择器沿用 e2e/plugins/mcp-center/mcp-center.spec.ts（隔离实例已 8/8 绿），
 * 保证「起源码走查」与「隔离实例 e2e」断言同一批 DOM 靶子。
 */

const PORT = process.env.E2E_FRONTEND_PORT ?? 'live-dev'
const OUT_DIR = path.resolve(
  fileURLToPath(new URL(`../../../screenshots/${PORT}`, import.meta.url)),
)
const ROUTE = '/mcp-center'

async function shot(page: Page, name: string): Promise<void> {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.png`)
  await page.screenshot({ path: file, fullPage: true })
  console.log(`[shot] ${file}`)
}

test.describe('起源码 dev-stack · mcp-center 走查', () => {
  test.describe.configure({ timeout: 180_000 })

  test('插件页渲染：版本徽标 + 工具表格 + 网关配置 + 工具计数', async ({ page }) => {
    const token = process.env.E2E_API_TOKEN?.trim()
    expect(token, '缺少 E2E_API_TOKEN：走查起源码须传 dev-stack 的明文 token').toBeTruthy()

    const errors: string[] = []
    page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`))
    page.on('console', (m) => {
      if (m.type() === 'error') errors.push(`console: ${m.text()}`)
    })

    await injectRealApiKey(page)
    await page.goto(`${frontendUrl()}${ROUTE}`, { waitUntil: 'domcontentloaded' })

    // ① 插件页渲染（插件前端 dist 被加载、路由已注册）
    await expect(page.getByRole('heading', { name: 'MCP 中心' }), 'MCP 中心 标题未渲染').toBeVisible({
      timeout: 60_000,
    })

    // ② 版本徽标（铁律 13：插件必展示版本；本批 McpCenter = 2.7.0）
    const badge = (await page.locator('.version-badge').innerText()).trim()
    console.log(`[badge] ${badge}`)
    expect(badge, '版本徽标未读到版本号').toMatch(/v\d+\.\d+\.\d+/)

    // ③ 工具表格：默认选中的是外部 MCP 服务器（Database/Filesystem/GitHub），其工具本就没有前缀；
    //    要验本批命名空间，必须切到「ForgeSelf Local」（= 宿主注册表）再看工具名。
    await expect(page.locator('.table-header'), '工具表格表头未渲染').toBeVisible({ timeout: 30_000 })
    await expect(page.locator('.table-row').first(), '工具表格无数据行').toBeVisible({ timeout: 30_000 })
    await page.locator('.server-item', { hasText: 'ForgeSelf Local' }).click()
    await expect(
      page.locator('.table-row').first(),
      '切到 ForgeSelf Local 后工具表格仍无数据',
    ).toBeVisible({ timeout: 30_000 })
    const localRows = await page.locator('.table-row').allInnerTexts()
    const prefixed = localRows
      .map((t) => t.replace(/\s+/g, ' ').trim())
      .filter((t) => /^(mcp_|dev_|workflow_|memory_|sems_|todo_|script_|design_|file_|text_|hub_|sys_|link_|schedule_)/.test(t))
    console.log(`[local-tools] 行数=${localRows.length} 带命名空间前缀=${prefixed.length}`)
    console.log(`[local-tools] 样例=${prefixed.slice(0, 5).join(' | ')}`)
    expect(prefixed.length, '本地 MCP 的工具名里没有带命名空间前缀的（本批改名未生效？）').toBeGreaterThan(0)

    // ④ 左栏 MCP 服务器清单里的工具计数（本批改名后注册表条目数）
    const sidebar = (await page.locator('aside, .mcp-servers, .sidebar').first().innerText().catch(() => '')).replace(
      /\s+/g,
      ' ',
    )
    const counts = [...sidebar.matchAll(/([^\d]+?)\s+(\d+)\s*个工具/g)].map((m) => `${m[1].trim()}=${m[2]}`)
    console.log(`[sidebar] ${counts.join(' | ') || sidebar.slice(0, 200)}`)
    const localMatch = sidebar.match(/ForgeSelf Local\s+(\d+)\s*个工具/)
    expect(localMatch, `左栏未读到本地 MCP 的工具计数（实际：${sidebar.slice(0, 200)}）`).toBeTruthy()
    const localCount = Number(localMatch?.[1] ?? '0')
    console.log(`[tools] ForgeSelf Local 暴露工具数 = ${localCount}`)
    expect(localCount, '本地 MCP 暴露的工具数异常偏少（改名后掉了一多半？）').toBeGreaterThan(100)

    await shot(page, 'mcp-center-walkthrough')

    // ⑤ 网关配置页签（第二个 tab）
    await page.getByRole('tab', { name: '网关配置' }).click()
    await expect(page.locator('.gateway-status-cards'), '网关配置页签未渲染').toBeVisible({ timeout: 30_000 })
    await shot(page, 'mcp-center-gateway')

    const fatal = errors.filter((e) => !/favicon|ResizeObserver/i.test(e))
    console.log(`[console-errors] ${fatal.length}`)
    expect(fatal, `控制台报错：\n${fatal.join('\n')}`).toHaveLength(0)
  })
})
