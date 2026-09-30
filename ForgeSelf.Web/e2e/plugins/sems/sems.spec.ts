import { test, expect, type Page, type Response } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { getRealApiKey, injectRealApiKey } from '../../helpers/real-auth'
import { backendUrl } from '../../helpers/e2e-env'

/**
 * 统一 e2e（插件层）：sems 插件（真实后端，零 mock）。
 * 复用 globalSetup 拉起的整套环境（宿主与前端 dev 端口由 PILOT-050 动态派生，临时数据目录全新）。
 *
 * 覆盖三组诉求（对应 docs/ai/pilot/sems-selfcontained-mcp-tools/02-spec.md AC13/AC14/AC15/AC16）：
 *  A. 远程加载冒烟 + 界面自洽证据（版本徽标、「添加项目」入口、不再把用户支去别的页面）
 *  B. 插件内部全生命周期：添加项目 → 刷新持久 → 加命令 → 启动 → 停止 → 移除（取消零请求 / 确认生效）
 *  C. 经 McpCenter 对外提供工具：list_tools 枚举到 13 个 sems_* 工具，且真调结果与界面数据一致
 *
 * 数据安全：临时项目目录**只创建不删除**（plugin-development 铁律 10）。
 */

const REPO_ROOT = path.resolve(fileURLToPath(new URL('../../../', import.meta.url)))

/** 动态读取插件清单：route 来自 plugin.json.frontend.route，version 用于徽标断言。 */
const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/Sems/plugin.json', import.meta.url)), 'utf-8'),
) as { Id: string; Version: string; frontend: { route: string } }
const PLUGIN_ROUTE = PLUGIN_MANIFEST.frontend.route
const PLUGIN_VERSION = PLUGIN_MANIFEST.Version

/** 远程入口 JS（清单 entry=web/dist/index.js）。 */
const ENTRY_RE = /\/plugins\/sems\/web\/dist\/index\.js(\?|\s|$)/

/** 取证产物目录（截图 + 网络/控制台日志 + MCP 协议日志）。 */
const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/sems', import.meta.url)))

/** 后端地址真源 = e2e-env（env → current.json → 默认回落），PILOT-050 起 spec 禁止硬编码端口。 */
const BACKEND_URL = backendUrl()
const AUTH_HEADERS = { Authorization: `Bearer ${getRealApiKey()}` }

/** spec FR-D1 的 13 个对外工具名。 */
const SEMS_TOOLS = [
  'sems_list_projects',
  'sems_register_project',
  'sems_update_project',
  'sems_remove_project',
  'sems_list_commands',
  'sems_add_command',
  'sems_update_command',
  'sems_delete_command',
  'sems_list_runs',
  'sems_check_runs',
  'sems_run_command',
  'sems_stop_command',
  'sems_stop_run',
]

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
    // 失败排查：记录所有非 2xx 的 /api 响应（401 抖动取证用）
    if (/\/api\//.test(url) && resp.status() >= 300) {
      evidence.consoleAll.push(`[http] ${resp.status()} ${resp.request().method()} ${url}`)
      if (resp.status() >= 300) evidence.consoleErrors.push(`[http] ${resp.status()} ${resp.request().method()} ${url}`)
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

/** 建一个真实存在的项目目录（带一个占位文件），返回绝对路径；绝不删除。 */
function makeProjectDir(label: string): string {
  const dir = path.join(REPO_ROOT, '.temp', 'e2e', 'sems-projects', `${Date.now()}-${label}`)
  mkdirSync(dir, { recursive: true })
  writeFileSync(path.join(dir, 'README.md'), `sems e2e project ${label}\n`, 'utf8')
  expect(existsSync(dir), `临时项目目录应已创建：${dir}`).toBe(true)
  return dir
}

/** 直连后端 API（用于与界面结果对账、以及收尾清理档案行）。 */
async function api<T>(p: string, method = 'GET', body?: unknown): Promise<T> {
  const res = await fetch(`${BACKEND_URL}${p}`, {
    method,
    headers: body ? { 'Content-Type': 'application/json', ...AUTH_HEADERS } : AUTH_HEADERS,
    body: body ? JSON.stringify(body) : undefined,
  })
  expect(res.ok, `${method} ${p} 应 2xx，实际 ${res.status}`).toBeTruthy()
  return (await res.json()) as T
}

interface ProjectsBody {
  success: boolean
  total: number
  projects: { id: number; root: string; name: string; source: string; commands: unknown[] }[]
}

// 文件内用例串行：4 个用例共享同一临时宿主的 SQLite 库，并行会撞写锁
// （实测 500：System.Data.SQLite.SQLiteException "database is locked"）
test.describe.configure({ mode: 'serial' })

/** ElMessageBox 点按钮（按文案）。 */
async function clickMessageBoxButton(page: Page, label: string): Promise<void> {
  const box = page.locator('.el-message-box')
  await expect(box).toBeVisible({ timeout: 10_000 })
  await box.locator('.el-message-box__btns button', { hasText: label }).click()
}

test.describe('统一 e2e（插件层）：sems 界面远程加载（真实后端，零 mock）', () => {
  test('远程插件界面渲染 + 入口 JS 200 + 标题 + 版本徽标 + 自洽入口 + 无致命报错', async ({ page }) => {
    test.setTimeout(90000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)

    // SPA 启动即拉 /api/plugin/frontend-manifest（鉴权端点）注册 /sems 路由，
    // 无 token 时 401 → 路由不注册 → main 空白（e2e 实抓教训，必须 goto 前注入）
    await injectRealApiKey(page)

    await page.goto(PLUGIN_ROUTE)

    const pluginRoot = page.locator('.sems')
    const errorPanel = page.locator('.plugin-view-state--error')
    const loadingPanel = page.locator('.plugin-view-state--loading')

    try {
      await expect(pluginRoot.or(errorPanel).or(loadingPanel)).toBeVisible({ timeout: 30000 })
    } catch (e) {
      // 失败取证：路由没挂上时 main 是空的，需要在超时前把现场抓下来再重抛
      const diag = await page.evaluate(async () => {
        const token = localStorage.getItem('forge_api_token')
        const manifest = await (async (): Promise<string> => {
          try {
            const res = await fetch('/api/plugin/frontend-manifest', {
              headers: token ? { Authorization: `Bearer ${token}` } : {},
            })
            const body = (await res.json()) as { data?: { id?: string }[] }
            return `${res.status} ids=${JSON.stringify((body.data ?? (body as unknown as { id?: string }[])).map?.((x) => x.id) ?? '(shape?)')}`
          } catch (err) {
            return `fetch 异常：${err instanceof Error ? err.message : String(err)}`
          }
        })()
        return {
          url: location.href,
          hasToken: !!token,
          manifest,
        }
      })
      dumpEvidence('sems-failure', evidence, [
        `URL: ${diag.url}`,
        `localStorage token: ${diag.hasToken}`,
        `frontend-manifest: ${diag.manifest}`,
      ])
      throw e
    }
    await expect(pluginRoot.or(errorPanel)).toBeVisible({ timeout: 30000 })

    const rendered = (await pluginRoot.count()) > 0
    const errorText = rendered ? '(未渲染错误占位)' : await errorPanel.innerText()

    await page.screenshot({ path: path.join(OUT_DIR, 'sems.png'), fullPage: true })

    const rootStyle = await page.evaluate(() => {
      const el = document.querySelector('.sems')
      if (!el) return '(no plugin root)'
      const s = getComputedStyle(el as Element)
      return `background=${s.backgroundColor} color=${s.color}`
    })

    const extra = [
      `渲染状态: ${rendered ? '插件根节点已渲染' : '渲染失败占位'}`,
      `错误占位内容: ${errorText.replace(/\n/g, ' | ')}`,
      `插件根节点计算样式: ${rootStyle}`,
    ]
    dumpEvidence('sems', evidence, extra)

    // ---- 断言 1：插件界面根节点真的渲染出来 ----
    expect(rendered, `插件界面未渲染，错误占位内容：${errorText}`).toBe(true)

    // ---- 断言 2：远程入口 JS 200 且 MIME 为 JS ----
    await expect
      .poll(() => evidence.network.find((l) => ENTRY_RE.test(l)) ?? '', {
        message: '未捕获到 /plugins/sems/web/dist/index.js 请求',
        timeout: 15000,
      })
      .toMatch(/^200 /)
    const entryHit = evidence.network.find((l) => ENTRY_RE.test(l)) as string
    expect(entryHit).toContain('javascript')

    // ---- 断言 3：标题 + 版本徽标（铁律 13：走查时能确认当前跑的是哪版）----
    await expect(page.locator('.sems__title')).toHaveText('软件工程管理系统', { timeout: 15000 })
    await expect(page.locator('.sems__version')).toHaveText(`v${PLUGIN_VERSION}`)
    // 这里只校验「徽标 == 清单版本」与版本号格式；「改了内容必须升版」属发布纪律，
    // 不在断言里写死具体版本字面量，否则每次正常升版都会把用例拖红。
    expect(PLUGIN_VERSION, 'plugin.json Version 必须是 x.y.z').toMatch(/^\d+\.\d+\.\d+$/)

    // ---- 断言 4：界面自洽入口存在，且不再把用户支去别的页面 ----
    await expect(page.getByRole('button', { name: '添加项目' }).first()).toBeVisible()
    const subText = await page.locator('.sems__sub').innerText()
    expect(subText).not.toMatch(/请前往.*AI Agent.*页/)

    // ---- 断言 5：无模块解析 / 组件解析 / 导出缺失类报错 ----
    const fatalPatterns = [
      /Failed to resolve component/i,
      /does not provide an export named/i,
      /Failed to (fetch|resolve) dynamically imported module/i,
      /Failed to load module script/i,
      /is not defined/i,
    ]
    const fatal = evidence.consoleErrors.filter((line) => fatalPatterns.some((re) => re.test(line)))
    expect(fatal, `控制台出现致命报错：\n${fatal.join('\n')}`).toEqual([])

    const box = await page.locator('.sems').boundingBox()
    expect(box, '插件根节点未渲染出可见尺寸').not.toBeNull()
    expect(box && box.width > 0 && box.height > 0, '插件根节点尺寸为 0，疑似未真正渲染').toBe(true)

    await page.screenshot({ path: path.join(OUT_DIR, 'sems-after.png'), fullPage: true })
  })
})

test.describe('统一 e2e（插件层）：sems 插件内项目全生命周期（添加→持久→命令→启停→移除）', () => {
  test('界面不依赖其他插件即可完成项目生命周期', async ({ page }) => {
    test.setTimeout(120000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence = attachCollectors(page)
    const dir = makeProjectDir('lifecycle')
    const projectName = path.basename(dir)

    // 记录 DELETE 请求，用于「取消 → 一个请求都不发」这条语义
    const deletes: string[] = []
    page.on('request', (req) => {
      if (req.method() === 'DELETE' && /\/api\/projects\/\d+/.test(req.url())) deletes.push(req.url())
    })

    await injectRealApiKey(page)
    await page.goto(PLUGIN_ROUTE)
    await expect(page.locator('.sems')).toBeVisible({ timeout: 30000 })

    // ---- 1. 添加项目（目录浏览弹层 + 手工输入绝对路径）----
    await page.getByRole('button', { name: '添加项目' }).first().click()
    const picker = page.locator('.dpick__box')
    await expect(picker).toBeVisible()
    // 首屏应已列举驱动器（browse 端点无参 → 盘符）
    await expect.poll(async () => (await picker.locator('.dpick__row').count()) > 0, { timeout: 15000 }).toBe(true)

    await picker.locator('.dpick__bar input').fill(dir)
    await picker.locator('.dpick__bar input').press('Enter')
    await expect(picker.locator('.dpick__current')).toContainText(dir, { timeout: 15000 })
    await page.screenshot({ path: path.join(OUT_DIR, 'sems-picker.png'), fullPage: true })

    await picker.getByRole('button', { name: '选择此目录' }).click()
    await expect(page.locator('.pcard__name', { hasText: projectName })).toBeVisible({ timeout: 20000 })
    await expect(picker).toHaveCount(0)

    // 登记来源必须是 manual（与 AIAgent 的 ai-agent 区分）
    const afterAdd = await api<ProjectsBody>('/api/projects')
    const mine = afterAdd.projects.find((p) => p.root === dir)
    expect(mine, '后端应能查到刚登记的项目').toBeTruthy()
    expect(mine!.source).toBe('manual')

    // ---- 2. 刷新页面仍在（点即落盘，无需再点保存）----
    await page.reload()
    await expect(page.locator('.pcard__name', { hasText: projectName })).toBeVisible({ timeout: 20000 })

    // ---- 3. 展开卡片并新增一条命令 ----
    // 统计是宿主全局数（并行用例的 MCP 登记也会计入），断言用「基线+1」而非绝对值
    const baselineCmds = (await api<ProjectsBody>('/api/projects')).projects.reduce(
      (n, p) => n + (p.commands?.length ?? 0),
      0,
    )
    await page.locator('.pcard', { hasText: projectName }).locator('[title="管理命令"]').click()
    await page.locator('.clist__add').click()
    const dlg = page.locator('.cmddlg__box')
    await expect(dlg).toBeVisible()
    await dlg.locator('.cmddlg__field input').nth(0).fill('e2e 常驻')
    await dlg.locator('.cmddlg__field input').nth(1).fill('ping -n 40 127.0.0.1')
    await dlg.getByRole('button', { name: '保存' }).click()
    await expect(dlg).toHaveCount(0)
    await expect(page.locator('.clist__name', { hasText: 'e2e 常驻' })).toBeVisible({ timeout: 15000 })

    // 回归守卫：GET /api/projects 的 commands 概要必须非空
    // （曾恒为空 → 统计「运行命令」0、快捷访问无图标、「启动全部」空转）
    await expect(page.locator('.sems__stat-num').nth(1)).toHaveText(String(baselineCmds + 1), { timeout: 15000 })

    // ---- 4. 启动 ----
    const runRespPromise = page.waitForResponse((r) => /\/api\/commands\/\d+\/run/.test(r.url()), { timeout: 20000 })
    await page.locator('.clist__op--run').click()
    const runResp = await runRespPromise
    const runBody = await runResp.text().catch(() => '(不可读)')
    expect(runResp.ok(), `启动接口应 2xx，实际 ${runResp.status()}，响应：${runBody}`).toBeTruthy()
    try {
      await expect(page.locator('.run__item')).toHaveCount(1, { timeout: 20000 })
    } catch (e) {
      // 失败取证：启动已 2xx 但列表空 → 分辨「服务端无会话」还是「前端不显示」
      const runsServer = await api<{ total?: number; runs?: unknown[] }>('/api/runs').catch((err) => String(err))
      dumpEvidence('sems-run-failure', evidence, [
        `POST /run 响应：${runBody}`,
        `GET /api/runs（直连后端）：${JSON.stringify(runsServer)}`,
      ])
      throw e
    }
    const runPid = await page.locator('.run__pid').innerText()
    expect(runPid).toMatch(/PID \d+/)
    await expect(page.locator('.sems__stat-num').nth(2)).toHaveText('1')
    await page.screenshot({ path: path.join(OUT_DIR, 'sems-running.png'), fullPage: true })

    // ---- 5. 停止（二次确认：确认路径）----
    await page.locator('.run__btn--stop').click()
    const confirmBox = page.locator('.el-message-box')
    await expect(confirmBox).toBeVisible()
    await expect(confirmBox.locator('.el-message-box__message')).toContainText('不可恢复')
    await clickMessageBoxButton(page, '确认停止')
    await expect(page.locator('.run__item')).toHaveCount(0, { timeout: 20000 })
    // 停止后统计卡「运行中」必须一起归零：面板内停止只刷新面板自身时，
    // 父级 runningCount 会停在旧值（2026-09-30 走查截图实抓：面板 0 / 统计仍 1）。
    await expect(page.locator('.sems__stat-num').nth(2)).toHaveText('0', { timeout: 10000 })

    // ---- 6. 移除项目：取消路径必须零请求 ----
    const deletesBefore = deletes.length
    await page.locator('.pcard', { hasText: projectName }).locator('[title="移除项目档案"]').click()
    const removeBox = page.locator('.el-message-box')
    await expect(removeBox).toBeVisible()
    // 文案必须写明不动磁盘（避免用户误以为删工程文件）
    await expect(removeBox.locator('.el-message-box__message')).toContainText('不会删除磁盘')
    await page.screenshot({ path: path.join(OUT_DIR, 'sems-remove-confirm.png'), fullPage: true })
    await clickMessageBoxButton(page, '取消')
    await expect(removeBox).toHaveCount(0)
    expect(deletes.length, '用户取消后不得发出任何 DELETE 请求').toBe(deletesBefore)
    await expect(page.locator('.pcard__name', { hasText: projectName })).toBeVisible()

    // ---- 7. 移除项目：确认路径生效并回到空态引导 ----
    await page.locator('.pcard', { hasText: projectName }).locator('[title="移除项目档案"]').click()
    await clickMessageBoxButton(page, '确认移除')
    await expect(page.locator('.pcard__name', { hasText: projectName })).toHaveCount(0, { timeout: 20000 })

    const afterRemove = await api<ProjectsBody>('/api/projects')
    expect(afterRemove.projects.find((p) => p.root === dir), '档案应已从库里删除').toBeFalsy()
    // 磁盘目录必须原样存在（只删档案）
    expect(existsSync(dir), '移除项目绝不能删除磁盘目录').toBe(true)

    await page.screenshot({ path: path.join(OUT_DIR, 'sems-removed.png'), fullPage: true })
  })
})

test.describe('统一 e2e（插件层）：sems 工具经 McpCenter 对外可达（真实 MCP 端口，零 mock）', () => {
  /** 网关端口由 McpCenter 运行时配置决定（mcp-center e2e 会临时改端口），故按 config 现值解析并等 /health 就绪。 */
  async function resolveMcpBase(): Promise<string> {
    const cfg = await api<{ data?: { listenUrl?: string } }>('/api/mcp-center/config')
    const base = cfg?.data?.listenUrl ?? `http://127.0.0.1:${process.env.FORGESELF_MCP_GATEWAY_PORT ?? '18889'}`
    const deadline = Date.now() + 60_000
    while (Date.now() < deadline) {
      try {
        const h = await fetch(`${base}/health`)
        if (h.ok) return base
      } catch {
        /* 热重启中 */
      }
      await new Promise((r) => setTimeout(r, 500))
    }
    throw new Error(`MCP 网关 /health 未在 60s 内就绪：${base}`)
  }

  async function mcpCall(base: string, method: string, params: unknown, id: number): Promise<unknown> {
    const res = await fetch(`${base}/mcp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' },
      body: JSON.stringify({ jsonrpc: '2.0', id, method, params }),
    })
    expect(res.ok, `MCP POST /mcp 应成功（${method}，HTTP ${res.status}）`).toBeTruthy()
    return (await res.json()) as unknown
  }

  function resultOf(resp: unknown): Record<string, unknown> {
    return (resp as { result: Record<string, unknown> }).result
  }

  /**
   * 万能工具调用 → 原始文本 + 解析结果。
   * 注意两种封套：宿主 list_tools 直出 `{total,keyword,tools[]}`；sems 工具出 `{success,data|error}`。
   * 失败时把**原始文本**放进断言消息，避免把「错误封套」误读成「空列表」。
   */
  async function callTool(base: string, tool: string, parameters: unknown, id: number): Promise<Record<string, unknown>> {
    const resp = await mcpCall(base, 'tools/call', { name: 'universal_tool', arguments: { tool, parameters } }, id)
    const result = resultOf(resp) as { content?: { text: string }[]; isError?: boolean }
    const raw = result.content?.[0]?.text ?? ''
    expect(result.isError ?? false, `${tool} 被网关判为错误，原始返回：${raw}`).toBe(false)
    let parsed: Record<string, unknown>
    try {
      parsed = JSON.parse(raw) as Record<string, unknown>
    } catch {
      throw new Error(`${tool} 返回不是 JSON，原始返回：${raw}`)
    }
    return parsed
  }

  /** sems 工具封套断言（成功路径），失败时带原始 error 文案。 */
  function unwrap(payload: Record<string, unknown>, tool: string): Record<string, unknown> {
    expect(payload.success, `${tool} 返回 success=false，原始返回：${JSON.stringify(payload)}`).toBe(true)
    return (payload.data ?? {}) as Record<string, unknown>
  }

  /** 从 list_tools 返回里取工具数组（两种封套都兼容）。 */
  function toolsOf(payload: Record<string, unknown>): { name: string; description?: string; parametersSchema?: unknown }[] {
    const direct = payload.tools
    if (Array.isArray(direct)) return direct as { name: string }[]
    const nested = (payload.data as Record<string, unknown> | undefined)?.tools
    if (Array.isArray(nested)) return nested as { name: string }[]
    throw new Error(`list_tools 返回里没有 tools 数组，原始返回：${JSON.stringify(payload)}`)
  }

  test('list_tools 能枚举 13 个 sems 工具，且真调结果与界面同一份数据', async () => {
    mkdirSync(OUT_DIR, { recursive: true })
    const base = await resolveMcpBase()
    const evidence: string[] = [`gateway: ${base}`]

    // 1) 对外仍然只有 universal_tool 一个入口（sems 接入不改变对外形态）
    const list = await mcpCall(base, 'tools/list', {}, 1)
    const tools = resultOf(list).tools as { name: string }[]
    expect(tools.map((t) => t.name)).toEqual(['universal_tool'])
    evidence.push(`tools/list: ${JSON.stringify(tools.map((t) => t.name))}`)

    // 2) 先证「外部确实能路由到 sems 工具」，再证「可被枚举发现」（顺序有意：前者是能力本身）
    const dir = makeProjectDir('mcp-chain')
    const registered = unwrap(
      await callTool(base, 'sems_register_project', { root: dir, name: 'MCP 登记的项目' }, 2),
      'sems_register_project',
    )
    const project = registered.project as { id: number; root: string; name: string; source: string }
    expect(project.root).toBe(dir)
    expect(project.name).toBe('MCP 登记的项目')
    expect(project.source).toBe('manual')
    evidence.push(`sems_register_project -> ${JSON.stringify(project)}`)

    // 3) 经 list_tools 发现 sems 工具（铁律 18：外部调用方要能发现能力）
    const discovery = await callTool(base, 'list_tools', { keyword: 'sems', includeSchema: true }, 3)
    const found = toolsOf(discovery)
    const names = found.map((t) => t.name).sort()
    for (const expected of SEMS_TOOLS) expect(names, `list_tools 应含 ${expected}`).toContain(expected)
    expect(names.filter((n) => n.startsWith('sems_'))).toHaveLength(SEMS_TOOLS.length)
    // schema 随枚举一起可见（外部据此知道怎么传参）
    const withSchema = found.find((t) => t.name === 'sems_register_project')
    expect(withSchema?.description, 'sems 工具说明应随 list_tools 返回').toBeTruthy()
    const schema = withSchema?.parametersSchema as { required?: string[] } | undefined
    expect(schema?.required, 'includeSchema=true 应带回参数 schema（含 required）').toContain('root')
    evidence.push(`list_tools keyword=sems: ${JSON.stringify(names)}`)

    // 4) 外部查询与界面/后端一致（同一份真相）
    const listed = unwrap(await callTool(base, 'sems_list_projects', {}, 4), 'sems_list_projects')
    const webView = await api<ProjectsBody>('/api/projects')
    expect(listed.total, '外部经 MCP 看到的项目数应与界面一致').toBe(webView.total)
    expect(
      (listed.projects as { id: number }[]).some((p) => p.id === project.id),
      '外部列表应含刚登记的项目',
    ).toBe(true)
    evidence.push(`sems_list_projects total=${String(listed.total)} / GET api/projects total=${webView.total}`)

    // 5) 命令闭环（外部只按 id 操作已登记对象）
    const added = unwrap(
      await callTool(base, 'sems_add_command', { projectId: project.id, name: 'mcp 命令', script: 'echo hi' }, 5),
      'sems_add_command',
    )
    const commandId = added.commandId as number
    const cmds = unwrap(await callTool(base, 'sems_list_commands', { projectId: project.id }, 6), 'sems_list_commands')
    expect((cmds.commands as { id: number }[]).map((c) => c.id)).toContain(commandId)

    expect((await callTool(base, 'sems_update_command', { commandId, name: 'mcp 命令改' }, 7)).success).toBe(true)
    expect((await callTool(base, 'sems_update_project', { id: project.id, type: 'tool', tags: 'e2e' }, 8)).success).toBe(true)
    expect((await callTool(base, 'sems_delete_command', { commandId }, 9)).success).toBe(true)

    // 6) 缺必填参数必须被拒（schema 的 required 真实生效）
    const missing = await mcpCall(base, 'tools/call', { name: 'universal_tool', arguments: { tool: 'sems_register_project', parameters: {} } }, 10)
    const missingResult = resultOf(missing) as { isError: boolean; content: { text: string }[] }
    expect(missingResult.isError).toBe(true)
    expect(missingResult.content[0].text).toMatch(/必填参数|参数验证失败|INVALID/i)
    evidence.push(`required 拒绝: ${missingResult.content[0].text}`)

    // 7) 运行类工具走查一遍语义（不真起进程：命令已删，启动应被拒且给出可读原因）
    const runGone = await mcpCall(base, 'tools/call', { name: 'universal_tool', arguments: { tool: 'sems_run_command', parameters: { commandId } } }, 11)
    const runGoneText = (resultOf(runGone) as { content: { text: string }[]; isError?: boolean }).content[0].text
    // 信封是 JSON 串（可能带 \uXXXX 转义），解析后断言而非裸子串匹配
    const runGoneEnv = JSON.parse(runGoneText) as { success: boolean; error: string }
    expect(runGoneEnv.success).toBe(false)
    expect(runGoneEnv.error).toContain('命令不存在')
    const runsList = unwrap(await callTool(base, 'sems_list_runs', {}, 12), 'sems_list_runs')
    expect(typeof runsList.total).toBe('number')
    evidence.push(`sems_run_command(已删命令): ${runGoneText} / list_runs total=${String(runsList.total)}`)

    // 8) 移除项目（级联清干净，不给后续会话留垃圾档案；磁盘目录保留）
    const removed = await callTool(base, 'sems_remove_project', { id: project.id }, 13)
    expect(removed.success, `sems_remove_project 失败，原始返回：${JSON.stringify(removed)}`).toBe(true)
    expect(existsSync(dir), '移除只删档案，磁盘目录必须还在').toBe(true)
    const finalView = await api<ProjectsBody>('/api/projects')
    expect(finalView.projects.find((p) => p.root === dir), '外部移除后界面也不应再看到该项目').toBeFalsy()
    evidence.push(`sems_remove_project ok, final total=${finalView.total}`)

    writeFileSync(path.join(OUT_DIR, 'mcp-tool-chain.log'), evidence.join('\n'), 'utf8')
    console.log(`\n[evidence] sems 工具经 MCP 对外链路通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
  })

  test('管理面鉴权：无令牌访问 sems 端点必须 401（含新增三端点）', async () => {
    const targets: Array<[string, string]> = [
      ['GET', '/api/projects'],
      ['POST', '/api/projects'],
      ['DELETE', '/api/projects/1'],
      ['GET', '/api/projects/browse'],
      ['GET', '/api/projects/1/commands'],
      ['GET', '/api/runs'],
      ['POST', '/api/runs/check'],
    ]
    for (const [method, p] of targets) {
      const res = await fetch(`${BACKEND_URL}${p}`, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: method === 'POST' ? '{}' : undefined,
      })
      expect(res.status, `${method} ${p} 未带令牌应被拒（401/403），实际 ${res.status}`).toBe(401)
    }
    // count 是首页统计卡的公开端点（既有 AllowAnonymous 设计），显式记录其现状
    const count = await fetch(`${BACKEND_URL}/api/projects/count`)
    expect(count.status).toBe(200)
  })
})
