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

/** 极简 MCP 客户端：发 JSON-RPC 请求（Streamable HTTP POST），返回响应 JSON。 */
async function mcpCall(method: string, params: unknown, id: number): Promise<unknown> {
  const res = await fetch(`${MCP_BASE}/mcp`, {
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

function errorOf(resp: unknown): { code: number; message: string } {
  return (resp as { error: { code: number; message: string } }).error
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
    const init = await mcpCall('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e' } }, 1)
    const initResult = resultOf(init)
    expect(initResult.protocolVersion).toBe('2025-06-18')
    expect((initResult.serverInfo as { name: string }).name).toBe('ForgeSelf McpCenter')
    expect(
      ((initResult.capabilities as { tools: { listChanged: boolean } }).tools as { listChanged: boolean }).listChanged,
    ).toBe(false)
    evidence.push(`initialize: ${JSON.stringify(initResult)}`)

    // 2.1) MCP 2.0（v2.2.0）：客户端声明 2025-11-25 → 服务端回显 2025-11-25（serverInfo 含 description）
    const init2 = await mcpCall('initialize', { protocolVersion: '2025-11-25', capabilities: {}, clientInfo: { name: 'e2e' } }, 6)
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

    // 网关地址 chip 显示监听地址
    await expect(page.locator('.gateway-address-chip')).toContainText('127.0.0.1')

    // 服务器列表（预置 4 台）
    await expect(page.locator('.server-item')).toHaveCount(4)

    // 工具管理 tab（默认激活）：表头 + 至少一行工具
    await expect(page.locator('.table-header')).toBeVisible()
    await expect(page.locator('.table-row').first()).toBeVisible()

    // 切到网关配置 tab：状态卡 + 修改表单
    await page.getByRole('tab', { name: '网关配置' }).click()
    await expect(page.locator('.gateway-status-cards')).toBeVisible()
    await expect(page.locator('.status-card')).toHaveCount(3)
    await expect(page.getByRole('heading', { name: '修改网关配置' })).toBeVisible()
    await expect(page.locator('.port-input')).toBeVisible()

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

    // mock 服务器脚本（仓库内固定路径，node 可执行）
    const mockScript = path.resolve(
      fileURLToPath(new URL('../../../../ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js', import.meta.url)),
    )

    /** 起一个 http/sse 模式 mock，返回监听端口与 kill。 */
    function startMock(mode: 'http' | 'sse'): Promise<{ port: number; kill: () => void }> {
      return new Promise((resolve, reject) => {
        const child = spawn('node', [mockScript, '--mode', mode], { stdio: ['ignore', 'pipe', 'pipe'] })
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
      await createServer({
        id: 'e2e-stdio', name: 'e2e stdio', transport: 'stdio', enabled: true,
        command: 'node', args: [mockScript, '--mode', 'stdio'],
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
})
