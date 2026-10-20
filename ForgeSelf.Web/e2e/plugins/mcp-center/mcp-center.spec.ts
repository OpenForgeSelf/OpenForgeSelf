import { expect } from '@playwright/test'
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'

// 插件层 fixture（beforeEach 注入真实 API token）
import { test } from '../../fixtures/e2e'
import { getRealApiKey } from '../../helpers/real-auth'

/**
 * 统一 e2e（插件层）：mcp-center（034 v2.1.0，前身 mcp-gateway + 宿主 mcp-tools 合并）。
 * 真实后端，零 mock。覆盖四条链路：
 *  1. 宿主侧：GET /api/plugin 返回 mcp-center（插件已发现并加载，旧 mcp-gateway 不再存在）
 *  2. MCP 服务端端口（FORGESELF_MCP_GATEWAY_PORT，生产默认 18890，e2e 走 worktree 派生 19000+）：
 *     - GET /health 探活（tools=1）
 *     - initialize → serverInfo.name = ForgeSelf McpCenter
 *     - tools/list → 恒 1 个工具 universal_tool（整体对外只有一个工具）
 *     - tools/call universal_tool → 转发真实目标工具 calculate，结果原样透传
 *     - tools/call 未知目标工具 → isError=true；未知 name → -32602
 *  3. 网关配置 API：GET /api/mcp-center/config（脱敏视图）→ PUT 改端口后热重启（改回原值）
 *  4. 外部 MCP 服务器接入（v2.1.0）：stdio / Streamable HTTP / HTTP+SSE 三传输配置→连接→工具→
 *     universal_tool 的 mcp.<服务器id>.<工具名> 转发（真连 node mock 服务器，零 mock 断言）
 *  5. 插件自带界面 /mcp-center：标题 + 版本徽标 + 服务器列表 + 工具表格 + 网关配置 tab（截图读图）
 *  6. 地址展示（v2.2.1 / PILOT-052）：界面展示与复制的必须是**可直连的端点** `listenUrl + /mcp`
 *     （含反向腿：根地址 404 ⇒ 少了 /mcp 就连不上，这是"地址必须带路径"的事实依据）
 */

const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../Plugins/McpCenter/plugin.json', import.meta.url)),
    'utf-8',
  ),
) as { Version: string; Id: string }

// 兼容旧 spec 的小写读取习惯：插件清单用 PascalCase（Version），后端 /api/plugin DTO 序列化为小写（version）
const PLUGIN_VERSION = PLUGIN_MANIFEST.Version

const MCP_PORT = Number(process.env.FORGESELF_MCP_GATEWAY_PORT ?? '18891')
const MCP_BASE = `http://127.0.0.1:${MCP_PORT}`
const BACKEND_URL = process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'

/** 管理面鉴权（v2.1.0+：api/mcp* / api/mcp-center/* 类级 [Authorize("ApiKeyPolicy")]）：统一带宿主令牌。 */
const AUTH_HEADERS = { Authorization: `Bearer ${getRealApiKey()}` }

/** 取证产物目录（协议日志 + 截图）。 */
const OUT_DIR = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/mcp-center', import.meta.url)))

/** 极简 MCP 客户端：发 JSON-RPC 请求（Streamable HTTP POST），返回响应 JSON。
 * 官方 SDK 应答可能是 SSE（event: message / data: {...}）或纯 JSON，两种都按协议解析。 */
async function mcpCall(method: string, params: unknown, id: number): Promise<unknown> {
  const res = await fetch(`${MCP_BASE}/mcp`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' },
    body: JSON.stringify({ jsonrpc: '2.0', id, method, params }),
  })
  expect(res.ok, `MCP POST /mcp 应成功（${method}，HTTP ${res.status}）`).toBeTruthy()
  return parseRpcJson(res, await res.text())
}

/** 解析 Streamable HTTP 应答体：纯 JSON 或 SSE（event: message / data: {...}）均为协议合法，统一取 JSON。 */
function parseRpcJson(res: Response, text: string): unknown {
  const ctype = res.headers.get('content-type') ?? ''
  const payload = ctype.includes('text/event-stream')
    ? text.split('\n').find((l) => l.startsWith('data:'))?.slice(5).trim()
    : text
  return JSON.parse(payload ?? text) as unknown
}

function resultOf(resp: unknown): Record<string, unknown> {
  return (resp as { result: Record<string, unknown> }).result
}

function errorOf(resp: unknown): { code: number; message: string } {
  return (resp as { error: { code: number; message: string } }).error
}

/**
 * 真实 MCP mock 服务器脚本（仓库内固定路径，node 可执行）。
 * 提供 echo{text:string} 与 add{a:number,b:number} 两个**带 inputSchema** 的工具，
 * 因此既能测协议连通，也能测工具测试台的「按 schema 动态渲染参数表单」。
 */
const MOCK_SCRIPT = path.resolve(
  fileURLToPath(new URL('../../../../ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js', import.meta.url)),
)

/** 起一个 http/sse 模式 mock，返回监听端口与 kill（供多个用例复用）。extraArgs 透传额外开关（如 --with-slow）。 */
function startMock(mode: 'http' | 'sse', extraArgs: string[] = []): Promise<{ port: number; kill: () => void }> {
  return new Promise((resolve, reject) => {
    const child = spawn('node', [MOCK_SCRIPT, '--mode', mode, ...extraArgs], { stdio: ['ignore', 'pipe', 'pipe'] })
    let out = ''
    const timer = setTimeout(() => {
      child.kill()
      reject(new Error(`mock ${mode} 启动超时`))
    }, 15_000)
    child.stdout.on('data', (d: Buffer) => {
      out += d.toString()
      const m = /LISTENING (\d+)/.exec(out)
      if (m) {
        clearTimeout(timer)
        resolve({ port: Number(m[1]), kill: () => { try { child.kill() } catch { /* 已退出 */ } } })
      }
    })
    child.on('exit', (code) => {
      clearTimeout(timer)
      reject(new Error(`mock ${mode} 提前退出 code=${code}`))
    })
  })
}

test.describe('统一 e2e（插件层）：mcp-center MCP 服务端 + 配置 API + 自带界面（真实后端，零 mock）', () => {
  // 3 个用例共享同一 MCP 端口（FORGESELF_MCP_GATEWAY_PORT 覆盖，e2e 默认 19000+），首用例临时改端口→改回：
  // 必须串行执行，否则并行用例会在端口切换窗口内互相踩（ECONNREFUSED）。
  test.describe.configure({ mode: 'serial' })

  test('宿主加载 mcp-center + MCP 端口全链路 + 网关配置 API', async () => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []

    // 0) 宿主侧：mcp-center 已加载，旧 mcp-gateway 已移除
    const pluginRes = await fetch(`${BACKEND_URL}/api/plugin`, { headers: AUTH_HEADERS })
    expect(pluginRes.ok).toBeTruthy()
    const pluginBody = (await pluginRes.json()) as {
      data?: { id: string; version?: string }[]
    }
    const ids = (pluginBody.data ?? []).map((p) => p.id)
    expect(ids, '宿主 /api/plugin 应包含 mcp-center').toContain('mcp-center')
    expect(ids, '旧 mcp-gateway 应已从宿主移除').not.toContain('mcp-gateway')
    const center = (pluginBody.data ?? []).find((p) => p.id === 'mcp-center')
    evidence.push(`host plugin: mcp-center v${center?.version ?? PLUGIN_VERSION}`)

    // 1) MCP 端口探活（宿主启动后插件 Apply 拉起监听，轮询 /health）
    let healthOk = false
    const deadline = Date.now() + 60_000
    while (!healthOk && Date.now() < deadline) {
      try {
        const h = await fetch(`${MCP_BASE}/health`)
        healthOk = h.ok
        if (healthOk) {
          const hb = (await h.json()) as { status: string; tools: number; version: string }
          expect(hb.status).toBe('ok')
          expect(hb.tools).toBe(1)
          evidence.push(`health: ${JSON.stringify(hb)}`)
        }
      } catch {
        /* 尚未就绪 */
      }
      if (!healthOk) await new Promise((r) => setTimeout(r, 1000))
    }
    expect(healthOk, `MCP 端口 /health 应在 60s 内就绪（${MCP_BASE}）`).toBeTruthy()

    // 2) initialize：协议版本 / 服务器信息（更名 ForgeSelf McpCenter）/ 能力
    const init = await mcpCall('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '1.0' } }, 1)
    const initResult = resultOf(init)
    expect(initResult.protocolVersion).toBe('2025-06-18')
    expect((initResult.serverInfo as { name: string }).name).toBe('ForgeSelf McpCenter')
    // tools 能力必须声明；listChanged 旗标由 SDK 自动推导（有状态=true、无状态=缺键），服务端实际从不推 list_changed（工具面恒静态）——见 034 契约
    expect((initResult.capabilities as { tools?: unknown }).tools).toBeDefined()
    evidence.push(`initialize: ${JSON.stringify(initResult)}`)

    // 2.1) MCP 2.0（v2.2.0）：客户端声明 2025-11-25 → 服务端回显 2025-11-25（serverInfo 含 description）
    const init2 = await mcpCall('initialize', { protocolVersion: '2025-11-25', capabilities: {}, clientInfo: { name: 'e2e', version: '1.0' } }, 6)
    const init2Result = resultOf(init2)
    expect(init2Result.protocolVersion).toBe('2025-11-25')
    expect((init2Result.serverInfo as { name: string; description?: string }).description).toBeTruthy()
    evidence.push(`initialize(2025-11-25): ${JSON.stringify(init2Result)}`)

    // 3) tools/list：恒 1 个工具，名 universal_tool
    const list = await mcpCall('tools/list', {}, 2)
    const tools = (resultOf(list).tools as { name: string }[])
    expect(tools).toHaveLength(1)
    expect(tools[0].name).toBe('universal_tool')
    expect((tools[0] as unknown as { inputSchema: { required: string[] } }).inputSchema.required).toContain('tool')
    evidence.push(`tools/list: ${JSON.stringify(tools.map((t) => t.name))}`)

    // 4) tools/call 转发真实目标工具 calculate（6*7=42）：结果原样透传
    const call = await mcpCall(
      'tools/call',
      { name: 'universal_tool', arguments: { tool: 'calculate', parameters: { expression: '6*7' } } },
      3,
    )
    const callResult = resultOf(call) as { content: { type: string; text: string }[]; isError: boolean }
    expect(callResult.isError).toBe(false)
    expect(callResult.content[0].type).toBe('text')
    const text = callResult.content[0].text
    expect(text).toContain('"success":true')
    expect(text).toContain('"result":42')
    evidence.push(`tools/call calculate(6*7): ${text}`)

    // 5) tools/call 未知目标工具 → isError=true 且带提示
    const unknown = await mcpCall('tools/call', { name: 'universal_tool', arguments: { tool: 'no_such_tool', parameters: {} } }, 4)
    const unknownResult = resultOf(unknown) as { content: { text: string }[]; isError: boolean }
    expect(unknownResult.isError).toBe(true)
    expect(unknownResult.content[0].text).toContain('unknown tool')
    evidence.push(`tools/call unknown target: ${unknownResult.content[0].text}`)

    // 6) tools/call 未知 name → JSON-RPC 错误 -32602（对外仅 1 个工具）
    const badName = await mcpCall('tools/call', { name: 'other_tool', arguments: {} }, 5)
    expect(errorOf(badName).code).toBe(-32602)
    evidence.push(`tools/call bad name: ${errorOf(badName).message}`)

    // 7) 网关配置 API：GET 脱敏视图
    const cfgRes = await fetch(`${BACKEND_URL}/api/mcp-center/config`, { headers: AUTH_HEADERS })
    expect(cfgRes.ok).toBeTruthy()
    const cfgBody = (await cfgRes.json()) as { data?: { listenUrl?: string; hasToken?: boolean; tokenMasked?: string; isRunning?: boolean } }
    expect(cfgBody.data?.listenUrl).toContain(`127.0.0.1:${MCP_PORT}`)
    expect(cfgBody.data?.hasToken).toBe(false)
    expect(cfgBody.data?.tokenMasked).toBe('')
    expect(cfgBody.data?.isRunning).toBe(true)
    evidence.push(`config GET: ${JSON.stringify(cfgBody.data)}`)

    // 8) 网关配置 API：PUT 改端口 → 热重启 → 新端口 health 就绪 → 改回原值
    const altPort = MCP_PORT + 1
    const putRes = await fetch(`${BACKEND_URL}/api/mcp-center/config`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
      body: JSON.stringify({ port: altPort }),
    })
    expect(putRes.ok, `PUT 网关配置应成功（HTTP ${putRes.status}）`).toBeTruthy()
    const putBody = (await putRes.json()) as { data?: { port?: number; isRunning?: boolean } }
    expect(putBody.data?.port).toBe(altPort)

    let altHealth = false
    const altDeadline = Date.now() + 30_000
    while (!altHealth && Date.now() < altDeadline) {
      try {
        const h = await fetch(`http://127.0.0.1:${altPort}/health`)
        altHealth = h.ok
      } catch {
        /* 重启中 */
      }
      if (!altHealth) await new Promise((r) => setTimeout(r, 500))
    }
    expect(altHealth, `热重启后新端口 ${altPort} /health 应就绪`).toBeTruthy()
    evidence.push(`config PUT: port -> ${altPort}, new port health ok`)

    // 改回原值（e2e 环境干净退出），并等待原端口 /health 重新就绪（供后续串行用例使用）
    const revert = await fetch(`${BACKEND_URL}/api/mcp-center/config`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
      body: JSON.stringify({ port: MCP_PORT }),
    })
    expect(revert.ok).toBeTruthy()
    let revertedHealth = false
    const revertDeadline = Date.now() + 30_000
    while (!revertedHealth && Date.now() < revertDeadline) {
      try {
        const h = await fetch(`${MCP_BASE}/health`)
        revertedHealth = h.ok
      } catch {
        /* 重启中 */
      }
      if (!revertedHealth) await new Promise((r) => setTimeout(r, 500))
    }
    expect(revertedHealth, `改回 ${MCP_PORT} 后 /health 应就绪`).toBeTruthy()
    evidence.push(`config PUT: port -> ${MCP_PORT} reverted, health ok`)

    // 证据落盘
    writeFileSync(path.join(OUT_DIR, 'mcp-protocol.log'), evidence.join('\n'), 'utf8')
    console.log(`\n[evidence] MCP 中心 e2e 全链路通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
  })

  test('插件自带界面 /mcp-center 渲染（双 tab + 版本徽标 + 工具表格 + 网关配置）', async ({ page }) => {
    await page.goto('/mcp-center')
    await page.waitForLoadState('networkidle')

    // 标题 + 版本徽标（铁律 13）
    await expect(page.getByRole('heading', { name: 'MCP 中心' })).toBeVisible()
    await expect(page.locator('.version-badge')).toContainText(`v${PLUGIN_VERSION}`)

    // 地址展示（v2.2.1 / PILOT-052）：界面给出的必须是**可直连的端点**（listenUrl + /mcp），
    // 不再是裸 host:port —— 根路径 404，照裸地址填客户端必然连不上（2026-09-27 输入16 的欠账）。
    const cfgUiRes = await fetch(`${BACKEND_URL}/api/mcp-center/config`, { headers: AUTH_HEADERS })
    expect(cfgUiRes.ok, `GET /api/mcp-center/config 应成功（HTTP ${cfgUiRes.status}）`).toBeTruthy()
    const cfgData = ((await cfgUiRes.json()) as {
      data?: { listenUrl?: string; listenHost?: string; port?: number }
    }).data
    const listenUrl = (cfgData?.listenUrl ?? '').trim().replace(/\/+$/, '')
    expect(listenUrl, 'config.listenUrl 应有值（网关运行中）').not.toBe('')
    const expectedEndpoint = `${listenUrl}/mcp`

    await expect(page.locator('.gateway-address-chip')).toHaveText(expectedEndpoint)
    expect(
      await page.locator('.gateway-address-chip').getAttribute('title'),
      'chip 的 title 也应是端点地址（悬停同样不误导）',
    ).toBe(expectedEndpoint)

    // 反向腿 + 正向腿：证明"少一段 /mcp 就连不上、带上就能连"
    /** 对任意地址发一次 JSON-RPC（同一份请求，仅换地址）。 */
    async function postRpc(url: string): Promise<Response> {
      return fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' },
        body: JSON.stringify({ jsonrpc: '2.0', id: 99, method: 'tools/list', params: {} }),
      })
    }
    const rootRes = await postRpc(`${listenUrl}/`)
    expect(rootRes.status, '根地址（缺少 /mcp）不是 MCP 端点 —— 界面不写路径必然误导').toBe(404)
    const endpointRes = await postRpc(expectedEndpoint)
    expect(endpointRes.status, '界面给出的端点地址必须可直接调用').toBe(200)
    const endpointBody = (parseRpcJson(endpointRes, await endpointRes.text())) as { result?: { tools?: { name: string }[] } }
    expect(endpointBody.result?.tools?.[0]?.name).toBe('universal_tool')
    console.log(
      `\n[evidence] 地址展示：config.listenUrl=${listenUrl} → 界面展示=${expectedEndpoint}；` +
        `根地址 ${rootRes.status} / 端点 ${endpointRes.status}（tools[0]=${endpointBody.result?.tools?.[0]?.name}）\n`,
    )

    // 服务器列表（预置 4 台）
    await expect(page.locator('.server-item')).toHaveCount(4)

    // 工具管理 tab（默认激活）：表头 + 至少一行工具
    await expect(page.locator('.table-header')).toBeVisible()
    await expect(page.locator('.table-row').first()).toBeVisible()

    // 切到网关配置 tab：状态卡 + 修改表单
    await page.getByRole('tab', { name: '网关配置' }).click()
    await expect(page.locator('.gateway-status-cards')).toBeVisible()
    await expect(page.locator('.status-card')).toHaveCount(3)

    // 「MCP 服务地址」卡（v2.2.1）：与 chip 同源端点 + 一行 /mcp 说明；运行状态卡仍暴露真实绑定地址
    await expect(page.locator('[data-mcp-url]')).toHaveText(expectedEndpoint)
    await expect(page.locator('[data-mcp-url]').locator('xpath=following-sibling::div[1]')).toContainText('/mcp')
    await expect(page.locator('.status-card').nth(1).locator('.card-sub')).toContainText(
      `监听 ${cfgData?.listenHost}:${cfgData?.port}`,
    )
    await expect(page.getByRole('heading', { name: '修改网关配置' })).toBeVisible()
    // v2.5.2 起「执行超时」输入复用 .port-input 样式类 → 同页两个，strict 模式取首个（网关端口行在前）
    await expect(page.locator('.port-input').first()).toBeVisible()

    // 外部服务器区块（v2.1.0）：标题 + 新增按钮 + 空态
    await expect(page.getByRole('heading', { name: '外部 MCP 服务器' })).toBeVisible()
    await expect(page.getByRole('button', { name: '新增服务器' })).toBeVisible()
    await expect(page.locator('.external-empty')).toContainText('尚未配置外部服务器')

    // 截图（视觉检查：图标/间距/对齐/溢出，Level 3）
    await page.screenshot({ path: path.join(OUT_DIR, 'gateway-tab.png') })
    await page.getByRole('tab', { name: '工具管理' }).click()
    await page.screenshot({ path: path.join(OUT_DIR, 'tools-tab.png') })
  })

  test('外部 MCP 服务器接入：三传输配置→连接→工具→universal_tool 转发', async () => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []

    // startMock / MOCK_SCRIPT 已提到模块级（工具测试台用例复用同一份 mock）
    const httpMock = await startMock('http')
    const sseMock = await startMock('sse')
    evidence.push(`mock http:${httpMock.port} / sse:${sseMock.port} 已启动`)

    const createdIds: string[] = []
    try {
      /** 配置外部服务器（POST /api/mcp-center/servers）。 */
      async function createServer(body: Record<string, unknown>): Promise<void> {
        const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
          body: JSON.stringify(body),
        })
        expect(res.ok, `POST 外部服务器 ${body.id} 应成功（HTTP ${res.status}）`).toBeTruthy()
        createdIds.push(body.id as string)
      }

      /** 轮询外部服务器列表直到 connected。 */
      async function waitConnected(id: string, timeoutMs = 30_000): Promise<{
        toolCount: number; protocolVersion?: string; connected: boolean
      }> {
        const deadline = Date.now() + timeoutMs
        while (Date.now() < deadline) {
          const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
          const body = (await res.json()) as { data?: { id: string; connected: boolean; toolCount: number; protocolVersion?: string }[] }
          const s = (body.data ?? []).find((x) => x.id === id)
          if (s?.connected) return s
          await new Promise((r) => setTimeout(r, 1000))
        }
        throw new Error(`外部服务器 ${id} 未在 ${timeoutMs}ms 内连接`)
      }

      // ── 1) stdio：node mock --mode stdio，工具 echo/add ──
      // framing:'lsp' 锁快路径（mock 说 LSP 帧）；auto 探测路径由本批「npx shim」用例在真实服务器上验证
      await createServer({
        id: 'e2e-stdio', name: 'e2e stdio', transport: 'stdio', enabled: true,
        command: 'node', args: [MOCK_SCRIPT, '--mode', 'stdio'], framing: 'lsp',
      })
      const s1 = await waitConnected('e2e-stdio')
      expect(s1.toolCount).toBe(2)
      evidence.push(`stdio 已连接：toolCount=${s1.toolCount} protocol=${s1.protocolVersion ?? '-'}`)

      // universal_tool mcp. 前缀转发 → 外部 add(3,4)=7
      const call1 = await mcpCall(
        'tools/call',
        { name: 'universal_tool', arguments: { tool: 'mcp.e2e-stdio.add', parameters: { a: 3, b: 4 } } },
        11,
      )
      const r1 = resultOf(call1) as { content: { text: string }[]; isError: boolean }
      expect(r1.isError).toBe(false)
      expect(r1.content[0].text).toContain('"sum":7')
      evidence.push(`universal_tool mcp.e2e-stdio.add(3,4) -> ${r1.content[0].text}`)

      // ── 2) streamable-http：单端点 /mcp ──
      await createServer({
        id: 'e2e-http', name: 'e2e http', transport: 'streamable-http', enabled: true,
        url: `http://127.0.0.1:${httpMock.port}/mcp`,
      })
      const s2 = await waitConnected('e2e-http')
      expect(s2.toolCount).toBe(2)
      evidence.push(`streamable-http 已连接：toolCount=${s2.toolCount}`)

      const call2 = await mcpCall(
        'tools/call',
        { name: 'universal_tool', arguments: { tool: 'mcp.e2e-http.echo', parameters: { text: '你好 HTTP' } } },
        12,
      )
      const r2 = resultOf(call2) as { content: { text: string }[]; isError: boolean }
      expect(r2.isError).toBe(false)
      expect(r2.content[0].text).toContain('你好 HTTP')
      evidence.push(`universal_tool mcp.e2e-http.echo -> ${r2.content[0].text}`)

      // ── 3) http-sse：GET /sse 发现 endpoint + POST /mcp（202 + SSE 流回传） ──
      await createServer({
        id: 'e2e-sse', name: 'e2e sse', transport: 'http-sse', enabled: true,
        url: `http://127.0.0.1:${sseMock.port}/sse`,
      })
      const s3 = await waitConnected('e2e-sse')
      expect(s3.toolCount).toBe(2)
      evidence.push(`http-sse 已连接：toolCount=${s3.toolCount}`)

      const call3 = await mcpCall(
        'tools/call',
        { name: 'universal_tool', arguments: { tool: 'mcp.e2e-sse.add', parameters: { a: 10, b: 32 } } },
        13,
      )
      const r3 = resultOf(call3) as { content: { text: string }[]; isError: boolean }
      expect(r3.isError).toBe(false)
      expect(r3.content[0].text).toContain('"sum":42')
      evidence.push(`universal_tool mcp.e2e-sse.add(10,32) -> ${r3.content[0].text}`)

      // ── 4) 坏格式错误提示（mcp.<id> 缺工具名段 → 格式提示） ──
      const bad = await mcpCall(
        'tools/call',
        { name: 'universal_tool', arguments: { tool: 'mcp.incomplete', parameters: {} } },
        14,
      )
      const rb = resultOf(bad) as { content: { text: string }[]; isError: boolean }
      expect(rb.isError).toBe(true)
      // error 字段为 JSON 转义文本，先 parse 再断言
      const errJson = JSON.parse(rb.content[0].text) as { error: string }
      expect(errJson.error).toContain('格式')
      evidence.push(`坏格式外部工具名错误：${errJson.error}`)

      writeFileSync(path.join(OUT_DIR, 'external-mcp.log'), evidence.join('\n'), 'utf8')
      console.log(`\n[evidence] 外部 MCP 接入 e2e 通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
    } finally {
      // 清理：删除配置的外部服务器 + 停掉 mock 进程（不留测试数据）
      for (const id of createdIds.reverse()) {
        try {
          await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}`, { method: 'DELETE', headers: AUTH_HEADERS })
        } catch {
          /* 清理失败不阻塞 */
        }
      }
      httpMock.kill()
      sseMock.kill()
    }
  })

  /**
   * PC-010 用户报障回归（2026-10-18）：外部 MCP 配 `npx @modelcontextprotocol/server-everything stdio`
   * 必须能连上——覆盖三层修复：① Windows 批处理 shim（npx.cmd）启动解析；② auto 帧协商
   * （官方 TS SDK 只答 NDJSON，旧客户端单说 LSP 帧永远超时）；③ null params 省略键（FastMCP 收
   * "params":null 完全沉默）。旧版应用层 e2e 的「stdio npx 优雅降级」分支掩盖了这三层全灭，
   * 本用例**零容忍：必须 connected**，不允许降级通过。
   */
  test('stdio npx 真实世界：npx 型外部 MCP 连接+工具列举+测试握手（v2.5.1，PC-010）', async ({ page }) => {
    // npx 首次可能拉包（本机 npm 缓存命中则秒级），预算放宽到 150s
    test.setTimeout(150_000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []
    const id = 'e2e-npx-shim'

    try {
      const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
        body: JSON.stringify({
          id, name: 'e2e npx shim', transport: 'stdio', enabled: true,
          command: 'npx', args: ['-y', '@modelcontextprotocol/server-everything', 'stdio'],
        }),
      })
      expect(res.ok, `POST ${id} 应成功（HTTP ${res.status}）`).toBeTruthy()
      evidence.push('npx 型外部服务器配置已保存')

      // 轮询到 connected（零容忍：无降级分支）
      const deadline = Date.now() + 120_000
      let connected: { connected: boolean; toolCount: number; lastError?: string } | undefined
      while (Date.now() < deadline) {
        const listRes = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
        const body = (await listRes.json()) as { data?: { id: string; connected: boolean; toolCount: number; lastError?: string }[] }
        connected = (body.data ?? []).find((x) => x.id === id)
        if (connected?.connected) break
        await new Promise((r) => setTimeout(r, 2000))
      }
      expect(connected?.connected, `npx 服务器应连上（lastError=${connected?.lastError ?? '-'}）`).toBeTruthy()
      expect((connected?.toolCount ?? 0), 'server-everything 应暴露工具集').toBeGreaterThan(0)
      evidence.push(`已连接：toolCount=${connected!.toolCount}`)

      // /test 真实握手（前端「测试」按钮同链路，验 data.success 契约+帧协商稳定）
      const testRes = await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}/test`, {
        method: 'POST', headers: AUTH_HEADERS,
      })
      const testBody = (await testRes.json()) as { data?: { success?: boolean; durationMs?: number; message?: string } }
      expect(testRes.status, `POST /test 应 200（实际 ${testRes.status} msg=${JSON.stringify(testBody)}）`).toBe(200)
      expect(testBody.data?.success).toBe(true)
      evidence.push(`测试握手成功：${testBody.data?.message ?? '-'}（${testBody.data?.durationMs ?? '-'}ms）`)

      // ── UI 层视觉取证（本批用户看得到的是页面）：列表已连接 + 点「测试」出成功 toast ──
      await page.goto('/mcp-center')
      await page.waitForLoadState('networkidle')
      await page.getByRole('tab', { name: '网关配置' }).click()
      const item = page.locator('.external-item', { hasText: 'e2e npx shim' })
      await expect(item).toContainText('已连接', { timeout: 30_000 })
      await expect(item).toContainText('个工具')
      // 断言不滚动；截图前把条目带入视口，否则证据图里没有 npx 条目（2026-10-18 读图发现）
      await item.scrollIntoViewIfNeeded()
      await page.screenshot({ path: path.join(OUT_DIR, 'npx-shim-list.png'), fullPage: false })
      evidence.push('UI：npx 条目已连接+工具数（截图 npx-shim-list.png）')

      // 测试按钮→成功 toast（前端文案「握手成功」；ElMessage 约 3s 自动销毁，断言要快）
      await item.getByRole('button', { name: '测试' }).click()
      const toast = page.locator('.el-message--success').first()
      await expect(toast).toContainText('握手成功', { timeout: 45_000 })
      await page.screenshot({ path: path.join(OUT_DIR, 'npx-shim-test-toast.png'), fullPage: false })
      evidence.push(`UI：测试 toast 原文=${(await toast.innerText()).trim()}（截图 npx-shim-test-toast.png）`)

      writeFileSync(path.join(OUT_DIR, 'npx-shim.log'), evidence.join('\n'), 'utf8')
      console.log(`\n[evidence] npx shim 回归通过：${evidence.join(' | ')}\n`)
    } finally {
      try {
        await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}`, { method: 'DELETE', headers: AUTH_HEADERS })
      } catch {
        /* 清理失败不阻塞 */
      }
    }
  })

  /**
   * 工具测试台（v2.3.0）：按 tools/list 的 inputSchema 动态渲染参数表单 → 填参 → 真实调用。
   * 判据分三层：① 表单由 schema 生成（必填标记 + number 类型字段）② 发送的 JSON 正确
   * ③ 结果面板出现耗时与真实返回文本（mock 的 add(3,4) = 7）。
   * 用仓库内 mock MCP 服务器（含 echo/add 两个带 schema 的工具），不依赖外网。
   */
  test('工具测试台：按 inputSchema 动态渲染参数表单并发起真实调用（v2.3.0）', async ({ page }) => {
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []
    const mock = await startMock('http')
    evidence.push(`mock http:${mock.port} 已启动`)

    const SERVER_ID = 'e2e-playground'
    try {
      // ── 1) 建一台指向真 mock 的外部服务器并等它连上 ──
      const created = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
        body: JSON.stringify({
          id: SERVER_ID, name: 'e2e playground', transport: 'streamable-http', enabled: true,
          url: `http://127.0.0.1:${mock.port}/mcp`,
        }),
      })
      expect(created.ok, `POST 外部服务器应成功（HTTP ${created.status}）`).toBeTruthy()

      let toolCount = 0
      const deadline = Date.now() + 30_000
      while (Date.now() < deadline) {
        const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
        const body = (await res.json()) as { data?: { id: string; connected: boolean; toolCount: number }[] }
        const s = (body.data ?? []).find((x) => x.id === SERVER_ID)
        if (s?.connected) { toolCount = s.toolCount; break }
        await new Promise((r) => setTimeout(r, 1000))
      }
      expect(toolCount, '应连上 mock 且拉到 2 个工具').toBe(2)
      evidence.push(`已连接：toolCount=${toolCount}`)

      // ── 2) 界面：打开工具测试台（外部服务器区块在「网关配置」页签，先切 tab） ──
      await page.goto('/mcp-center')
      await page.waitForLoadState('networkidle')
      await page.getByRole('tab', { name: '网关配置' }).click()
      await page.locator('.external-item', { hasText: 'e2e playground' })
        .getByRole('button', { name: '工具' })
        .click()
      const playground = page.getByTestId('tool-playground')
      await expect(playground).toBeVisible()

      // 工具清单来自真实 tools/list：echo / add
      await expect(page.getByTestId('tp-tool-add')).toBeVisible()
      await expect(page.getByTestId('tp-tool-echo')).toBeVisible()
      evidence.push('工具清单渲染：add / echo')

      // ── 3) 选 add：schema 声明 a、b 两个 number 且都必填 ⇒ 表单必须出现两个必填字段 ──
      await page.getByTestId('tp-tool-add').click()
      await expect(page.getByTestId('tp-selected')).toHaveText('add')
      await expect(page.getByTestId('tp-field-a')).toBeVisible()
      await expect(page.getByTestId('tp-field-b')).toBeVisible()
      await expect(page.getByTestId('tp-required-a')).toBeVisible()
      await expect(page.getByTestId('tp-required-b')).toBeVisible()
      evidence.push('schema→表单：add 的 a/b 两个必填字段已渲染')

      // 必填未填时调用被拦（不会出现"点了没反应"）
      await page.getByTestId('tp-invoke').click()
      await expect(page.getByTestId('tp-result')).toHaveCount(0)

      // 填参：a=3, b=4（el-input-number 的输入框）
      await page.getByTestId('tp-field-a').locator('input').fill('3')
      await page.getByTestId('tp-field-b').locator('input').fill('4')
      const preview = await page.getByTestId('tp-preview').textContent()
      expect(JSON.parse((preview ?? '{}').trim()) as Record<string, number>).toEqual({ a: 3, b: 4 })
      evidence.push(`将发送：${(preview ?? '').trim()}`)

      // ── 4) 发起真实调用 ──
      await page.getByTestId('tp-invoke').click()
      const resultPanel = page.getByTestId('tp-result')
      await expect(resultPanel).toBeVisible({ timeout: 30_000 })
      await expect(page.getByTestId('tp-result-text')).toContainText('"sum":7')
      const elapsed = (await page.getByTestId('tp-elapsed').textContent()) ?? ''
      expect(Number.parseInt(elapsed, 10)).toBeGreaterThanOrEqual(0)
      evidence.push(`调用结果：${(await page.getByTestId('tp-result-text').textContent()) ?? ''}（${elapsed.trim()}）`)

      // 原始 JSON 可展开
      await page.getByRole('button', { name: '查看原始 JSON' }).click()
      await expect(page.getByTestId('tp-result-raw')).toContainText('"isError":false')

      await page.screenshot({ path: path.join(OUT_DIR, 'tool-playground.png') })

      writeFileSync(path.join(OUT_DIR, 'tool-playground.log'), evidence.join('\n'), 'utf8')
      console.log(`\n[evidence] 工具测试台 e2e 通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
    } finally {
      try {
        await fetch(`${BACKEND_URL}/api/mcp-center/servers/${SERVER_ID}`, {
          method: 'DELETE', headers: AUTH_HEADERS,
        })
      } catch { /* 清理失败不阻塞 */ }
      mock.kill()
    }
  })

  /**
   * 外部 MCP UI 主链路冒烟（PC-008 修复批，2026-10-17）：复现批三缺陷的用户路径。
   * 新增对话框点「填入 DeepWiki 预设」（预设 id/url 后改指本地 mock，不依赖外网）→ 保存自动连接
   * → 列表「已连接」+ 点「测试」出「测试通过」toast（①②路径）→ 点「编辑」URL 回显真值 +
   * 脱敏说明可见 + 原样保存走 PUT 更新链（③路径 + 掩码不提交防护）→ 删除清理。
   */
  test('外部 MCP UI 主链路：预设新增→连接→测试→编辑回显→删除（v2.5.0）', async ({ page }) => {
    // 链路含建连轮询 + reload + 真实握手，30s 默认预算不够（2026-10-17 修复批实证）
    test.setTimeout(180_000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []
    const mock = await startMock('http')
    const mockUrl = `http://127.0.0.1:${mock.port}/mcp`
    const SERVER_ID = 'e2e-ui-main'
    const SERVER_NAME = 'e2e UI 主链路'

    /** 当前打开的新增/编辑对话框（append-to-body） */
    const formDialog = (title: string) => page.locator('.el-dialog', { hasText: title })
    /** 对话框内指定 label 行的输入框 */
    const formInput = (dlg: ReturnType<typeof formDialog>, label: string) =>
      dlg.locator('.external-form-row', { hasText: label }).locator('input').first()

    try {
      // ── 1) 新增：走用户路径点「填入 DeepWiki 预设」，再把地址改指本地 mock ──
      await page.goto('/mcp-center')
      await page.waitForLoadState('networkidle')
      await page.getByRole('tab', { name: '网关配置' }).click()
      await page.getByRole('button', { name: '新增服务器' }).click()

      const createDlg = formDialog('新增外部服务器')
      await expect(createDlg).toBeVisible()
      await createDlg.getByTestId('fill-deepwiki-preset').click()
      // 预设生效：id 填入 deepwiki，传输切到 Streamable HTTP
      await expect(formInput(createDlg, 'ID')).toHaveValue('deepwiki')
      await expect(createDlg.locator('.external-form-row', { hasText: '传输' })).toContainText('Streamable HTTP')
      evidence.push('预设按钮：id=deepwiki + 传输切到 Streamable HTTP')

      await formInput(createDlg, 'ID').fill(SERVER_ID)
      await formInput(createDlg, '名称').fill(SERVER_NAME)
      await formInput(createDlg, 'URL').fill(mockUrl)
      await createDlg.getByRole('button', { name: '保存' }).click()
      evidence.push(`已提交新增（enabled ⇒ 后端自动建连）：${mockUrl}`)

      // ── 2) 等后端真实连上 mock（initialize+tools/list），再刷新页面核列表状态 ──
      let connected = false
      const deadline = Date.now() + 30_000
      while (Date.now() < deadline) {
        const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
        const body = (await res.json()) as { data?: { id: string; connected: boolean; toolCount: number }[] }
        const s = (body.data ?? []).find((x) => x.id === SERVER_ID)
        if (s?.connected) { connected = true; break }
        await new Promise((r) => setTimeout(r, 1000))
      }
      expect(connected, '自动建连应在 30s 内完成（clientInfo 缺失时这里永远等不到）').toBe(true)

      await page.reload()
      await page.waitForLoadState('networkidle')
      await page.getByRole('tab', { name: '网关配置' }).click()
      const item = page.locator('.external-item', { hasText: SERVER_NAME })
      await expect(item).toContainText('已连接')
      await expect(item).toContainText('2 个工具')
      evidence.push('列表状态：已连接 + 2 个工具')

      // ── 3) 点「测试」→ 握手成功 message（缺陷②的用户路径；成功反馈是 ElMessage 非 test-toast） ──
      await item.getByRole('button', { name: '测试' }).click()
      await expect(page.locator('.el-message--success').first()).toContainText('握手成功', { timeout: 30_000 })
      evidence.push('测试按钮：真实握手（initialize+ping）通过')

      // ── 4) 点「编辑」→ URL 回显真值 + 脱敏说明可见（缺陷③的用户路径）；原样保存走 PUT 更新链 ──
      await item.getByRole('button', { name: '编辑' }).click()
      const editDlg = formDialog(`编辑 ${SERVER_NAME}`)
      await expect(editDlg).toBeVisible()
      await expect(formInput(editDlg, 'URL')).toHaveValue(mockUrl)
      await expect(formInput(editDlg, 'ID')).toHaveValue(SERVER_ID)
      await expect(editDlg.locator('.external-form-secret-hint').first()).toBeVisible()
      evidence.push(`编辑回显：URL=${mockUrl}，脱敏说明可见`)

      // 原样保存：headers/env 与脱敏基线一致 ⇒ 前端不提交该字段，后端合并保留 ⇒ 更新成功且不丢配置
      await editDlg.getByRole('button', { name: '保存' }).click()
      await expect(page.locator('.el-message--success').first()).toContainText('已更新', { timeout: 15_000 })
      const afterPut = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
      const putBody = (await afterPut.json()) as { data?: { id: string; url: string }[] }
      expect(putBody.data?.find((x) => x.id === SERVER_ID)?.url).toBe(mockUrl)
      evidence.push('编辑保存（PUT）成功且 url 未丢')

      await page.screenshot({ path: path.join(OUT_DIR, 'external-mcp-ui.png') })

      // ── 5) 删除清理（confirm 弹窗 accept） ──
      page.once('dialog', (d) => { void d.accept() })
      await item.getByRole('button', { name: '删除' }).click()
      await expect(page.locator('.external-item', { hasText: SERVER_NAME })).toHaveCount(0, { timeout: 15_000 })
      evidence.push('UI 删除：列表项已移除')

      writeFileSync(path.join(OUT_DIR, 'external-mcp-ui.log'), evidence.join('\n'), 'utf8')
      console.log(`\n[evidence] 外部 MCP UI 主链路 e2e 通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
    } finally {
      try {
        await fetch(`${BACKEND_URL}/api/mcp-center/servers/${SERVER_ID}`, {
          method: 'DELETE', headers: AUTH_HEADERS,
        })
      } catch { /* 清理失败不阻塞 */ }
      mock.kill()
    }
  })

  /**
   * 外部 MCP 执行超时可配置（v2.5.2，PILOT 2026-10-18）：配置真实驱动执行预算的双档验证。
   * ① PUT executionTimeoutMs=5s → GET 回显（往返）；② 新建连的慢工具调用在 5s 被掐断（isError 带
   * 「请求超时（5s）」）；③ 改 60s 后重连同工具同参（延时 15s）放行——证明非写死、且对
   * 新建/重连连接生效（Lazy 语义）。零 mock：真起 node mock 服务器（--with-slow）。
   */
  test('外部 MCP 执行超时可配置：5s 档掐断慢工具、60s 档放行（v2.5.2）', async ({ page }) => {
    // 含 15s 真实延时调用 + 两次建连轮询，预算放宽
    test.setTimeout(240_000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []
    const id = 'e2e-timeout'
    const mock = await startMock('http', ['--with-slow'])
    evidence.push(`mock http:${mock.port} 已启动（--with-slow）`)

    /** PUT 网关配置并取回视图 data（执行超时往返通道）。 */
    async function putConfig(body: Record<string, unknown>): Promise<Record<string, unknown>> {
      const res = await fetch(`${BACKEND_URL}/api/mcp-center/config`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
        body: JSON.stringify(body),
      })
      expect(res.ok, `PUT config ${JSON.stringify(body)} 应成功（HTTP ${res.status}）`).toBeTruthy()
      const j = (await res.json()) as { data?: Record<string, unknown> }
      return j.data ?? {}
    }

    /** 建一台指向 slow mock 的外部服务器并轮询到 connected。 */
    async function createAndConnect(): Promise<void> {
      const created = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
        body: JSON.stringify({
          id, name: 'e2e timeout', transport: 'streamable-http', enabled: true,
          url: `http://127.0.0.1:${mock.port}/mcp`,
        }),
      })
      expect(created.ok, `POST 外部服务器 ${id} 应成功（HTTP ${created.status}）`).toBeTruthy()
      const deadline = Date.now() + 30_000
      while (Date.now() < deadline) {
        const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
        const body = (await res.json()) as { data?: { id: string; connected: boolean; toolCount: number }[] }
        const s = (body.data ?? []).find((x) => x.id === id)
        if (s?.connected) {
          expect(s.toolCount, 'slow mock 应暴露 echo/add/slow 三工具').toBe(3)
          return
        }
        await new Promise((r) => setTimeout(r, 1000))
      }
      throw new Error(`外部服务器 ${id} 未在 30s 内连接`)
    }

    /** 通过 universal_tool 转发调用 mcp.<id>.slow(ms)。 */
    async function callSlow(ms: number, rpcId: number): Promise<{ content: { text: string }[]; isError: boolean }> {
      const call = await mcpCall(
        'tools/call',
        { name: 'universal_tool', arguments: { tool: `mcp.${id}.slow`, parameters: { ms } } },
        rpcId,
      )
      return resultOf(call) as { content: { text: string }[]; isError: boolean }
    }

    try {
      // 基线：默认 300000（v2.5.2 内置默认）
      const baseRes = await fetch(`${BACKEND_URL}/api/mcp-center/config`, { headers: AUTH_HEADERS })
      const base = ((await baseRes.json()) as { data?: { executionTimeoutMs?: number } }).data
      expect(base?.executionTimeoutMs, '默认执行超时应为 300000ms').toBe(300000)
      evidence.push(`基线 executionTimeoutMs=${base?.executionTimeoutMs}`)

      // ① 配置往返：PUT 5000 → 响应/GET 都回显 5000
      const putShort = await putConfig({ executionTimeoutMs: 5000 })
      expect(putShort.executionTimeoutMs).toBe(5000)
      const getShort = await fetch(`${BACKEND_URL}/api/mcp-center/config`, { headers: AUTH_HEADERS })
      const getShortBody = ((await getShort.json()) as { data?: { executionTimeoutMs?: number } }).data
      expect(getShortBody?.executionTimeoutMs, 'GET 应回显新超时').toBe(5000)
      evidence.push('往返：PUT 5000 → 响应/GET 均回显 5000')

      // ② 5s 预算下新建连接 → 15s 慢工具被掐断（报错带实际秒数）
      await createAndConnect()
      const short = await callSlow(15000, 21)
      expect(short.isError, '5s 档慢工具应超时（不允许降级通过）').toBe(true)
      // error 封套为 JSON 转义文本（中文被 \u 编码），先 parse 再断言（与坏格式用例同法）
      const shortErr = JSON.parse(short.content[0].text) as { error: string }
      expect(shortErr.error).toContain('请求超时')
      expect(shortErr.error).toContain('（5s）')
      evidence.push(`5s 档掐断：${shortErr.error}`)

      // ③ 越界拒绝守卫：PUT 999ms → 400（既有 ArgumentException 映射）
      const badPut = await fetch(`${BACKEND_URL}/api/mcp-center/config`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
        body: JSON.stringify({ executionTimeoutMs: 999 }),
      })
      expect(badPut.status, '越界 PUT 应 400').toBe(400)
      evidence.push('越界 PUT 999ms → HTTP 400')

      // ④ 改 60s 后重连（DELETE+POST 重建传输，Lazy 语义） → 同工具同参放行
      const putLong = await putConfig({ executionTimeoutMs: 60000 })
      expect(putLong.executionTimeoutMs).toBe(60000)
      await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}`, { method: 'DELETE', headers: AUTH_HEADERS })
      await createAndConnect()
      const long = await callSlow(15000, 22)
      expect(long.isError, '60s 档同参应放行').toBe(false)
      expect(long.content[0].text).toContain('"ok":true')
      evidence.push(`60s 档放行：${long.content[0].text}`)

      // ⑤ UI 取证：网关配置页「执行超时」输入框回显 60（60000ms→秒换算）；截图供读图（走查门禁 ③）
      await page.goto('/mcp-center')
      await page.waitForLoadState('networkidle')
      await page.getByRole('tab', { name: '网关配置' }).click()
      const timeoutRow = page.locator('.form-row', { hasText: '执行超时' })
      await expect(timeoutRow.locator('input').first()).toHaveValue('60')
      await expect(timeoutRow.locator('.form-hint')).toBeVisible()
      await timeoutRow.scrollIntoViewIfNeeded()
      await page.screenshot({ path: path.join(OUT_DIR, 'execution-timeout-ui.png') })
      evidence.push('UI：执行超时输入框回显 60（=60000ms），hint 可见（截图 execution-timeout-ui.png）')

      writeFileSync(path.join(OUT_DIR, 'execution-timeout.log'), evidence.join('\n'), 'utf8')
      console.log(`\n[evidence] 执行超时可配置 e2e 通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
    } finally {
      // 恢复默认超时并清理服务器/mock（e2e 环境干净退出；超时恢复对后续用例无影响）
      try {
        await putConfig({ executionTimeoutMs: 300000 })
      } catch { /* 不阻塞 */ }
      try {
        await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}`, { method: 'DELETE', headers: AUTH_HEADERS })
      } catch { /* 清理失败不阻塞 */ }
      mock.kill()
    }
  })

  /**
   * 外部 MCP 工具进 list_tools 枚举（v2.6.0，PILOT-072）：决策 D10 修订——「枚举可见 + 调用可达，注册表隔离保持」。
   * 零 mock 真连：node mock（http）建连 → 网关 universal_tool{tool:"list_tools"} 同次应答出现外部全名
   * （mcp.<id>.<名>、source=mcp-external、pluginId=服务器 id、includeSchema 带参数 schema）→
   * 枚举到的全名原样直调成功（枚举即可用闭环）→ disconnect 后枚举即不得再出现该条目（可见性跟随连接态）。
   */
  test('list_tools 枚举含外部工具全名并立即可调（v2.6.0）', async () => {
    test.setTimeout(120_000)
    mkdirSync(OUT_DIR, { recursive: true })
    const evidence: string[] = []
    const id = 'e2e-listmerge'
    const mock = await startMock('http')
    evidence.push(`mock http:${mock.port} 已启动`)

    /** 网关侧调 universal_tool（目标工具结果在 content[0].text 里原样返回）。 */
    async function callUniversal(tool: string, parameters: Record<string, unknown>, rpcId: number) {
      const resp = await mcpCall('tools/call', { name: 'universal_tool', arguments: { tool, parameters } }, rpcId)
      return resultOf(resp) as { content: { text: string }[]; isError: boolean }
    }

    try {
      // ① 建连（POST 即自动连接，同外部接入用例通道）
      const created = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...AUTH_HEADERS },
        body: JSON.stringify({
          id, name: 'e2e list merge', transport: 'streamable-http', enabled: true,
          url: `http://127.0.0.1:${mock.port}/mcp`,
        }),
      })
      expect(created.ok, `POST 外部服务器 ${id} 应成功（HTTP ${created.status}）`).toBeTruthy()
      const deadline = Date.now() + 30_000
      let connected = false
      while (Date.now() < deadline && !connected) {
        const res = await fetch(`${BACKEND_URL}/api/mcp-center/servers`, { headers: AUTH_HEADERS })
        const body = (await res.json()) as { data?: { id: string; connected: boolean }[] }
        connected = (body.data ?? []).find((x) => x.id === id)?.connected ?? false
        if (!connected) await new Promise((r) => setTimeout(r, 1000))
      }
      expect(connected, `外部服务器 ${id} 未在 30s 内连接`).toBeTruthy()

      // ② list_tools 枚举：按全名关键字过滤（零 mock 下宿主工具全量太多，过滤段就是本用例的被测面）
      const listed = await callUniversal('list_tools', { keyword: `mcp.${id}`, includeSchema: true }, 31)
      expect(listed.isError, 'list_tools 不应报错').toBe(false)
      const lv = JSON.parse(listed.content[0].text) as {
        total: number
        tools: { name: string; description: string; pluginId: string; source: string; parametersSchema?: Record<string, unknown> }[]
      }
      expect(lv.total, '已连接应枚举出外部 2 工具').toBe(2)
      expect(lv.tools.map((t) => t.name).sort()).toEqual([`mcp.${id}.add`, `mcp.${id}.echo`])
      for (const t of lv.tools) {
        expect(t.source, '外部条目 source 必须是 mcp-external').toBe('mcp-external')
        expect(t.pluginId, '外部条目 pluginId = 服务器 id').toBe(id)
        expect(t.parametersSchema, 'includeSchema 时外部条目应带参数 schema').toBeTruthy()
      }
      evidence.push(`list_tools 枚举：${lv.tools.map((t) => `${t.name}(source=${t.source})`).join(', ')}`)

      // ③ 枚举即可用闭环：拿枚举到的全名原样直调（本批立项动机：不然添加了用不了）
      const invoked = await callUniversal(`mcp.${id}.add`, { a: 3, b: 4 }, 32)
      expect(invoked.isError, '枚举出的全名应可直接调用').toBe(false)
      expect(invoked.content[0].text).toContain('"sum":7')
      evidence.push(`全名直调 mcp.${id}.add(3,4) -> ${invoked.content[0].text}`)

      // ④ 可见性跟随连接态：disconnect 后同一枚举不得再出现外部条目（宁缺勿崩，回退仅宿主工具）
      const disc = await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}/disconnect`, { method: 'POST', headers: AUTH_HEADERS })
      expect(disc.ok, `POST disconnect ${id} 应成功（HTTP ${disc.status}）`).toBeTruthy()
      const afterDisc = await callUniversal('list_tools', { keyword: `mcp.${id}` }, 33)
      const av = JSON.parse(afterDisc.content[0].text) as { total: number }
      expect(av.total, '断开后外部条目应即从枚举中消失').toBe(0)
      evidence.push('disconnect 后 list_tools 外部条目归零（可见性跟随连接态）')

      writeFileSync(path.join(OUT_DIR, 'list-tools-external.log'), evidence.join('\n'), 'utf8')
      console.log(`\n[evidence] list_tools 外部枚举 e2e 通过，证据 ${evidence.length} 行\n${evidence.join('\n')}\n`)
    } finally {
      try {
        await fetch(`${BACKEND_URL}/api/mcp-center/servers/${id}`, { method: 'DELETE', headers: AUTH_HEADERS })
      } catch { /* 清理失败不阻塞 */ }
      mock.kill()
    }
  })
})
