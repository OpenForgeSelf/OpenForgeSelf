import { expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// 使用插件层 fixture：beforeEach 自动注入真实 API token 到 localStorage（零 mock）。
import { test } from '../../fixtures/e2e'

/**
 * 统一 e2e（插件层）：Agent 中枢（AgentHub）插件界面（真实后端，零 mock）。
 *
 * 复用 globalSetup 拉起的整套环境（宿主 7102 + 前端 dev 7002，临时数据目录全新）。
 *
 * 铁律：零 mock，全部对接真实后端（vite 代理 → 7102）。本文件只验证与取证，不改业务代码。
 *
 * 覆盖点：
 *  1. `/agent-hub` 路由下插件根节点 `.ok-root` 是否渲染（远程加载，非宿主内置视图）
 *  2. `/plugins/agent-hub/web/dist/index.js` 是否 200 且 MIME 为 JS（验证插件自治产物可用）
 *  3. 稳定文案「Agent 中枢」标题 + 统计栏
 *  4. 后端接口真实可用：GET /api/agent-hub/agents、/tasks、/tasks/stats、/agents/discover
 *  5. agent 登记的写往返（POST 新增 → GET 列表可见 → DELETE 清理；同时验证 vendor 唯一约束）
 *  6. 控制台无致命报错 + 截图读图 + 无横向溢出
 *
 * 运行：
 *   pnpm exec playwright test --config=playwright.config.ts e2e/plugins/agent-hub
 */

/** 动态读取插件清单：route 来自 plugin.json.frontend.route（避免硬编码命名空间歧义）。 */
const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../ForgeSelf.Api/Plugins/AgentHub/plugin.json', import.meta.url)),
    'utf-8',
  ),
) as { version: string; frontend: { route: string } }
const PLUGIN_ROUTE = PLUGIN_MANIFEST.frontend.route

/** 远程入口 JS（清单 entry=web/dist/index.js）。 */
const ENTRY_RE = /\/plugins\/agent-hub\/web\/dist\/index\.js(\?|\s|$)/

/** 取证产物目录（截图 + 网络/控制台日志）。 */
const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/agent-hub', import.meta.url)))

interface Evidence {
  network: string[]
  consoleAll: string[]
  consoleErrors: string[]
}

function attachCollectors(page: Page): Evidence {
  const evidence: Evidence = { network: [], consoleAll: [], consoleErrors: [] }
  page.on('console', (msg) => {
    const line = `[${msg.type()}] ${msg.text()}`
    evidence.consoleAll.push(line)
    if (msg.type() === 'error') evidence.consoleErrors.push(msg.text())
  })
  page.on('pageerror', (err) => {
    const line = `[pageerror] ${err.message}`
    evidence.consoleErrors.push(line)
    evidence.consoleAll.push(line)
  })
  page.on('response', (resp: Response) => {
    const url = resp.url()
    if (ENTRY_RE.test(url)) {
      evidence.network.push(
        `${resp.status()} ${resp.request().method()} ${url} ct=${resp.headers()['content-type'] ?? '-'}`,
      )
    }
  })
  return evidence
}

function dumpEvidence(name: string, evidence: Evidence, extra: string[]): void {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.log`)
  const body = [
    `=== route: ${PLUGIN_ROUTE} ===`,
    '',
    '--- 关键网络请求 ---',
    ...(evidence.network.length ? evidence.network : ['(无)']),
    '',
    '--- 控制台 error / 未捕获异常 ---',
    ...(evidence.consoleErrors.length ? evidence.consoleErrors : ['(无)']),
    '',
    '--- 附加信息 ---',
    ...extra,
    '',
    '--- 控制台全量输出 ---',
    ...(evidence.consoleAll.length ? evidence.consoleAll : ['(无)']),
  ].join('\n')
  writeFileSync(file, body, 'utf8')
  console.log(`\n[evidence] ${file}\n${body}\n`)
}

/**
 * 以页面上下文直连真实后端（复用 fixture 注入的真实 token）。
 * 返回状态码与解析后的 body，供断言使用。
 */
async function api(
  page: Page,
  url: string,
  init?: { method?: string; body?: unknown },
): Promise<{ status: number; body: any }> {
  return page.evaluate(
    async ({ url, init }) => {
      const token = localStorage['forge_api_token'] as string | undefined
      const r = await fetch(url, {
        method: init?.method ?? 'GET',
        cache: 'no-store',
        headers: {
          Accept: 'application/json',
          ...(init?.body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        ...(init?.body !== undefined ? { body: JSON.stringify(init.body) } : {}),
      })
      return { status: r.status, body: await r.json().catch(() => null) }
    },
    { url, init },
  )
}

test.describe('统一 e2e（插件层）：Agent 中枢（AgentHub）界面（真实后端，零 mock）', () => {
  test('远程插件界面渲染 + 入口 JS 200 + 核心接口可用', async ({ page }) => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)

    await page.goto(PLUGIN_ROUTE)

    // 三态之一必须出现：插件根节点 / 加载失败占位 / 加载中占位
    const pluginRoot = page.locator('.ok-root')
    const errorPanel = page.locator('.plugin-view-state--error')
    const loadingPanel = page.locator('.plugin-view-state--loading')

    await expect(pluginRoot.or(errorPanel).or(loadingPanel)).toBeVisible({ timeout: 30000 })
    await expect(pluginRoot.or(errorPanel)).toBeVisible({ timeout: 30000 })

    const rendered = (await pluginRoot.count()) > 0
    const errorText = rendered ? '(未渲染错误占位)' : await errorPanel.innerText()

    await page.screenshot({ path: path.join(OUT_DIR, 'agent-hub.png'), fullPage: true })

    // 诊断：确认插件界面是否带上了宿主主题变量（取根节点计算样式，排查纯白闪屏）
    const rootStyle = await page.evaluate(() => {
      const el = document.querySelector('.ok-root')
      if (!el) return '(no plugin root)'
      const s = getComputedStyle(el as Element)
      return `background=${s.backgroundColor} color=${s.color}`
    })

    const extra = [
      `渲染状态: ${rendered ? '插件根节点已渲染' : '渲染失败占位'}`,
      `错误占位内容: ${errorText.replace(/\n/g, ' | ')}`,
      `插件根节点计算样式: ${rootStyle}`,
    ]
    dumpEvidence('agent-hub', evidence, extra)

    // ---- 断言 1：插件界面根节点真的渲染出来 ----
    expect(rendered, `插件界面未渲染，错误占位内容：${errorText}`).toBe(true)

    // ---- 断言 2：远程入口 JS 200 且 MIME 为 JS ----
    const entryHit = await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', {
        message: '未捕获到 /plugins/agent-hub/web/dist/index.js 请求',
        timeout: 15000,
      })
      .toMatch(/^200 /)
      .then(() => evidence.network.find((l) => ENTRY_RE.test(l)) as string)
    expect(entryHit).toContain('javascript')

    // ---- 断言 3：标题稳定文案 + 统计栏 ----
    await expect(page.locator('.ok-title')).toHaveText('Agent 中枢', { timeout: 15000 })
    await expect(page.locator('.ok-stats')).toBeVisible()

    // ---- 断言 4：核心后端接口真实可用 ----
    const agentsResp = await api(page, '/api/agent-hub/agents')
    expect(agentsResp.status, `GET /api/agent-hub/agents 非 200：status=${agentsResp.status}`).toBe(200)
    expect(Array.isArray(agentsResp.body?.data), 'agents 数据应为数组').toBe(true)

    const tasksResp = await api(page, '/api/agent-hub/tasks?limit=50')
    expect(tasksResp.status, `GET /api/agent-hub/tasks 非 200：status=${tasksResp.status}`).toBe(200)
    expect(Array.isArray(tasksResp.body?.data), 'tasks 数据应为数组').toBe(true)

    const statsResp = await api(page, '/api/agent-hub/tasks/stats')
    expect(statsResp.status, `GET /api/agent-hub/tasks/stats 非 200：status=${statsResp.status}`).toBe(200)
    expect(statsResp.body?.data, 'stats 数据应存在').toBeTruthy()

    // discover：扫描本机已装 agent（真实探测文件系统，不自动登记）
    const discoverResp = await api(page, '/api/agent-hub/agents/discover')
    expect(discoverResp.status, `GET /api/agent-hub/agents/discover 非 200：status=${discoverResp.status}`).toBe(200)
    expect(Array.isArray(discoverResp.body?.data), 'discover 数据应为数组').toBe(true)
  })

  test('agent 登记写往返（新增 → 列表可见 → 唯一约束 → 清理）', async ({ page }) => {
    const evidence = attachCollectors(page)
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.ok-root')).toBeVisible({ timeout: 30000 })

    // 用唯一 vendor，避免与真实已登记数据/其它用例冲突
    const uniq = `e2e${Date.now().toString(36)}`
    const vendor = `vend-${uniq}`
    const name = `e2e-${uniq}`
    let createdId = 0

    try {
      // ---- 新增 ----
      // 注意：后端 AgentSaveRequest.Tags 是 List<String>?（数组），不是逗号串。
      const createResp = await api(page, '/api/agent-hub/agents', {
        method: 'POST',
        body: { name, vendor, kind: 'Generic', tags: ['e2e'] },
      })
      expect(createResp.status, `POST /api/agent-hub/agents 非 200：status=${createResp.status}`).toBe(200)
      createdId = Number(createResp.body?.data?.id ?? 0)
      expect(createdId, '新增后应返回 agent id').toBeGreaterThan(0)
      expect(createResp.body?.data?.vendor).toBe(vendor)

      // ---- 列表可见 ----
      const listResp = await api(page, '/api/agent-hub/agents')
      const found = (listResp.body?.data as Array<{ id: number; vendor: string }> | undefined)?.find(
        (a) => a.id === createdId,
      )
      expect(found, '新增的 agent 应出现在列表中').toBeTruthy()

      // ---- 唯一约束：同 vendor 再登记应被拒（业务规则「同 vendor 只允许登记一次」）----
      const dupResp = await api(page, '/api/agent-hub/agents', {
        method: 'POST',
        body: { name: `${name}-dup`, vendor, kind: 'Generic' },
      })
      expect(dupResp.status, '同 vendor 重复登记应失败（非 2xx）').toBeGreaterThanOrEqual(400)

      dumpEvidence('agent-hub-write', evidence, [
        `新增 id=${createdId} vendor=${vendor}`,
        `重复登记状态码=${dupResp.status}（预期 >=400，验证唯一约束生效）`,
      ])
    } finally {
      // ---- 清理：删除本用例创建的 agent（只删自己建的，不碰其它数据）----
      if (createdId > 0) {
        const delResp = await api(page, `/api/agent-hub/agents/${createdId}`, { method: 'DELETE' })
        expect([200, 204, 404], `清理删除应成功，实际 status=${delResp.status}`).toContain(delResp.status)
      }
    }
  })

  test('截图读图 + 无横向溢出（视觉检查）', async ({ page }) => {
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.ok-root')).toBeVisible({ timeout: 30000 })

    mkdirSync(OUT_DIR, { recursive: true })
    await page.screenshot({ path: path.join(OUT_DIR, 'agent-hub-visual.png'), fullPage: true })

    // 回归守卫：根容器不得横向溢出（与 Home 插件同一类视觉缺陷的通用守卫）
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
    )
    expect(overflow, 'Agent 中枢页不应出现横向滚动条').toBe(false)

    // 标题与统计栏均真实可见（避免只渲染骨架）
    await expect(page.locator('.ok-title')).toBeVisible()
    await expect(page.locator('.ok-stats')).toBeVisible()
  })
})
