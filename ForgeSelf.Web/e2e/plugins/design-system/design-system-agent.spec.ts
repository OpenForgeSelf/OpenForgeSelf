import { expect } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { test } from '../../fixtures/e2e'
import { getRealApiKey } from '../../helpers/real-auth'

/**
 * M1 Agent 工具层 e2e（design-system v2.8.0，真实宿主 + 真实插件库，零 mock）。
 * 六条断言（03-plan §e2e / AC26）：
 *  1. 宿主加载 design-system，meta.modelVersion == plugin.json.Version（版本自洽，防旧产物）
 *  2. MCP 网关 tools/list 探活 + universal_tool list_tools 枚举 8 个 design_*
 *  3. design_context 经网关取 semantic.surface-bg hex == REST tokens/effective（同源）
 *  4. design_review 反例命中硬编码 → summary.errors ≥ 1
 *  5. design_create 干跑(apply=false) 不落库 → apply=true 落库 → 审计无 critical
 *  6. PUT agent-access 关写 → design_edit 被拒 → 收尾改回 → design_edit 恢复可写
 * 网关直连写法沿用 mcp-center.spec.ts（Streamable HTTP POST /mcp）。
 */
const MCP_PORT = Number(process.env.FORGESELF_MCP_GATEWAY_PORT ?? '18889')
const MCP_BASE = `http://127.0.0.1:${MCP_PORT}`
const BACKEND_URL = process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'
const AUTH_HEADERS = { Authorization: `Bearer ${getRealApiKey()}` }
const DS_API = `${BACKEND_URL}/api/design-system`

const PLUGIN_MANIFEST = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../../Plugins/DesignSystem/plugin.json', import.meta.url)),
    'utf-8',
  ),
) as { Version: string }
const DS_VERSION = PLUGIN_MANIFEST.Version

let _mcpId = 10
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

/** tools/call universal_tool 的文本结果（content[0].text，目标工具原样 JSON）。 */
function callText(resp: unknown): string {
  const r = resultOf(resp) as { content?: { text: string }[]; isError?: boolean }
  return r.content?.[0]?.text ?? ''
}

/** 经网关调 design_* 工具并解包目标工具结果 JSON；转发失败即红。 */
async function dsTool(tool: string, parameters: Record<string, unknown>): Promise<Record<string, unknown>> {
  const resp = await mcpCall(
    'tools/call',
    { name: 'universal_tool', arguments: { tool, parameters } },
    ++_mcpId,
  )
  const r = resultOf(resp) as { content?: { text: string }[]; isError?: boolean }
  expect(r.isError, `universal_tool 转发 ${tool} 应成功：${callText(resp)}`).toBe(false)
  const envelope = JSON.parse(callText(resp)) as Record<string, unknown>
  expect(envelope.success, `design_${tool} 工具自身应 success：${callText(resp)}`).toBe(true)
  return envelope
}

/** 解出工具封套的 data 载荷（{success,data} → data）。 */
function dataOf(envelope: Record<string, unknown>): Record<string, unknown> {
  return envelope.data as Record<string, unknown>
}

test.describe('设计系统 Agent 工具层（M1 v2.8.0）：网关枚举 + 同源 + 写开关（真实后端，零 mock）', () => {
  test.describe.configure({ mode: 'serial' })

  let projectCode = ''
  let projectId = 0
  let surfaceBgHex = ''

  test('宿主加载 design-system 且版本自洽（modelVersion == plugin.json.Version）', async () => {
    const pluginRes = await fetch(`${BACKEND_URL}/api/plugin`, { headers: AUTH_HEADERS })
    expect(pluginRes.ok).toBeTruthy()
    const body = (await pluginRes.json()) as { data?: { id: string; version?: string }[] }
    const ids = (body.data ?? []).map((p) => p.id)
    expect(ids, '宿主 /api/plugin 应包含 design-system').toContain('design-system')

    const metaRes = await fetch(`${DS_API}/meta`, { headers: AUTH_HEADERS })
    expect(metaRes.ok).toBeTruthy()
    const meta = ((await metaRes.json()) as { data: { modelVersion: string; agentTools: string[] } }).data
    expect(meta.modelVersion, 'meta.modelVersion 必须等于 plugin.json.Version（防旧产物）').toBe(DS_VERSION)
    expect(meta.agentTools, 'meta.agentTools 应枚举 8 个 design_* 工具').toEqual([
      'design_guide',
      'design_context',
      'design_lookup',
      'design_review',
      'design_audit',
      'design_presets',
      'design_create',
      'design_edit',
    ])
  })

  test('MCP 网关 list_tools 枚举 8 个 design_* 工具', async () => {
    let healthOk = false
    const deadline = Date.now() + 60_000
    while (!healthOk && Date.now() < deadline) {
      try {
        const h = await fetch(`${MCP_BASE}/health`)
        if (h.ok) {
          const hb = (await h.json()) as { status: string; tools: number }
          expect(hb.status).toBe('ok')
          expect(hb.tools).toBe(1) // 对外恒 1 个 universal_tool
          healthOk = true
        }
      } catch {
        /* 尚未就绪 */
      }
      if (!healthOk) await new Promise((r) => setTimeout(r, 1000))
    }
    expect(healthOk, `MCP 网关 /health 应在 60s 内就绪（端口 ${MCP_PORT}）`).toBe(true)

    const list = await mcpCall('tools/list', {}, 2)
    const tools = (resultOf(list).tools as { name: string }[]).map((t) => t.name)
    expect(tools).toContain('universal_tool')

    // list_tools 是枚举助手（响应无 {success,data} 封套），直接解 content[0].text
    const enumerated = JSON.parse(await callText(await mcpCall(
      'tools/call',
      { name: 'universal_tool', arguments: { tool: 'list_tools', parameters: { keyword: 'design' } } },
      ++_mcpId,
    ))) as Record<string, unknown>
    const text = JSON.stringify(enumerated)
    for (const name of [
      'design_guide',
      'design_context',
      'design_lookup',
      'design_review',
      'design_audit',
      'design_presets',
      'design_create',
      'design_edit',
    ]) {
      expect(text, `list_tools 应枚举 ${name}`).toContain(name)
    }
  })

  test('design_context 经网关与 REST tokens/effective 同源（semantic.surface-bg hex 逐位相等）', async () => {
    const ts = Date.now().toString(36)
    const res = await fetch(`${DS_API}/projects/quick-create`, {
      method: 'POST',
      headers: { ...AUTH_HEADERS, 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: `e2e 网关同源 ${ts}`, code: `e2e-src-${ts}`, kind: 'console', preset: 'admin-calm', dryRun: false }),
    })
    expect(res.ok, 'REST quick-create 应成功').toBeTruthy()
    const created = ((await res.json()) as { data: { project: { code: string }; uiRoute: string } }).data
    expect(created.project.code).toBeTruthy()
    expect(created.uiRoute).toContain('/design-system') // design_create 的 uiRoute 同源（可跳界面）
    projectCode = created.project.code

    const projectsRes = await fetch(`${DS_API}/projects?keyword=${encodeURIComponent(projectCode)}`, { headers: AUTH_HEADERS })
    const projects = ((await projectsRes.json()) as { data: { id: number; code: string }[] }).data
    projectId = projects.find((p) => p.code === projectCode)!.id
    expect(projectId).toBeGreaterThan(0)

    const effRes = await fetch(`${DS_API}/projects/${projectId}/tokens/effective?theme=light`, { headers: AUTH_HEADERS })
    expect(effRes.ok).toBeTruthy()
    const effBody = (await effRes.json()) as {
      data: {
        theme: string
        themeId: number
        count: number
        items: { path: string; colorHex: string | null }[]
      }
    }
    const tokens = effBody.data.items
    const surface = tokens.find((t) => t.path === 'semantic.surface-bg')
    expect(surface?.colorHex, 'REST effective 应有 semantic.surface-bg 的 colorHex').toBeTruthy()
    surfaceBgHex = surface!.colorHex ?? ''

    const ctx = await dsTool('design_context', { project: projectCode, theme: 'light', sections: ['colors'] })
    const ctxData = dataOf(ctx)
    const text = JSON.stringify(ctxData)
    expect(ctxData.theme).toBe('light')
    expect(text, 'design_context 输出应含 semantic.surface-bg 且带 hex').toContain('semantic.surface-bg')
    expect(text).toContain(surfaceBgHex)
  })

  test('design_review 反例命中硬编码 → summary.hardcoded ≥ 1（strict 升 error）', async () => {
    const review = await dsTool('design_review', {
      project: projectCode,
      code: 'body { background: #123456 }',
      language: 'css',
      strict: true,
    })
    const reviewData = dataOf(review)
    const summary = reviewData.summary as { errors: number; hardcoded: number }
    expect(summary.hardcoded, '硬编码色应被数出来').toBeGreaterThanOrEqual(1)
    expect(summary.errors, 'strict=true 时 hardcoded warning 应升为 error（§C3）').toBeGreaterThanOrEqual(1)
    expect(reviewData.findings).toBeTruthy()
  })

  test('design_create 干跑不落库 → apply 落库 → 审计无 critical', async () => {
    const name = `e2e 网关创建 ${Date.now().toString(36)}`
    const dry = await dsTool('design_create', { name, kind: 'console', preset: 'admin-calm', apply: false })
    const dryData = dataOf(dry)
    expect(dryData.applied).toBe(false)
    expect(dryData.preview).toBeTruthy()
    const dryProjects = ((await (await fetch(`${DS_API}/projects`, { headers: AUTH_HEADERS })).json()) as {
      data: { code: string }[]
    }).data
    expect(dryProjects.some((p) => (dryData.preview as { project: { code: string } }).project?.code === p.code),
      '干跑(apply=false) 不得落库').toBe(false)

    const real = await dsTool('design_create', { name, kind: 'console', preset: 'admin-calm', apply: true })
    const realData = dataOf(real)
    expect(realData.applied).toBe(true)
    const realCode = (realData.project as { code: string }).code
    expect(realCode).toBeTruthy()

    const audit = await dsTool('design_audit', { project: realCode, run: true })
    const auditData = dataOf(audit)
    const summary = auditData.summary as { critical: number; blocking: boolean }
    expect(summary.critical, '生成产物审计不得有 critical（自查表 #24 ①）').toBe(0)
    expect(summary.blocking).toBe(false)
  })

  test('PUT agent-access 关写 → design_edit 拒 → 收尾改回 → 恢复可写', async () => {
    const getRes = await fetch(`${DS_API}/agent-access`, { headers: AUTH_HEADERS })
    const before = ((await getRes.json()) as { data: { allowWrite: boolean } }).data
    expect(before.allowWrite, '初始写开关应为开（默认 fail-open）').toBe(true)

    const closeRes = await fetch(`${DS_API}/agent-access`, {
      method: 'PUT',
      headers: { ...AUTH_HEADERS, 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled: false }),
    })
    expect(closeRes.ok).toBeTruthy()
    const closed = ((await closeRes.json()) as { data: { allowWrite: boolean } }).data
    expect(closed.allowWrite).toBe(false)

    const editResp = await mcpCall(
      'tools/call',
      {
        name: 'universal_tool',
        arguments: {
          tool: 'design_edit',
          parameters: { project: projectCode, action: 'set_token', path: 'semantic.surface-bg', value: surfaceBgHex },
        },
      },
      ++_mcpId,
    )
    const editResult = resultOf(editResp) as { content?: { text: string }[]; isError?: boolean }
    const editText = editResult.content?.[0]?.text ?? ''
    const parsed = JSON.parse(editText) as { success?: boolean; error?: string }
    expect(parsed.success, `关写后 design_edit 必须被拒：${editText}`).toBe(false)
    expect(parsed.error ?? '', '拒绝文案应说明写开关').toContain('写入已被关闭')

    const reopenRes = await fetch(`${DS_API}/agent-access`, {
      method: 'PUT',
      headers: { ...AUTH_HEADERS, 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled: true }),
    })
    expect(reopenRes.ok).toBeTruthy()
    const reopened = ((await reopenRes.json()) as { data: { allowWrite: boolean } }).data
    expect(reopened.allowWrite, '收尾必须改回写开关（幂等改回原值）').toBe(true)

    const okEdit = await dsTool('design_edit', {
      project: projectCode,
      action: 'set_token',
      path: 'semantic.surface-bg',
      value: surfaceBgHex,
    })
    expect(okEdit.success ?? true, `重开写开关后 design_edit 应可写：${JSON.stringify(okEdit)}`).toBe(true)
  })
})
