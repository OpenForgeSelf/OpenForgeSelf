import { test, expect, type APIRequestContext } from '@playwright/test'
import { mkdirSync, rmSync, existsSync, statSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { resolveHostApiToken } from '../../helpers/host-api-token'

/**
 * AIAgent 端到端执行测试（正式、可重复、零 mock）。
 *
 * 目标：验证 agent 在真实 LLM 驱动下能否真正「跑完一个任务」——调用 write_file/read_file 工具
 * 并把文件写到磁盘、再读回来（这是此前临时脚本无法验证、且 UI 启发式误判的核心能力缺口）。
 *
 * 设计要点（对比已被否定的临时 .cjs 脚本）：
 * - 直连真实运行实例（默认 51888，已配 LM Studio provider + 本次根因修复），零 mock；
 * - 用**同步** `POST /api/ai-agent/chat`：响应直接带回完整 `toolCalls` 列表，天然判定
 *   「agent 是否真的调了 write_file/read_file」，不再用「发送键可用 + 文本长度」不可靠启发式；
 * - 断言对象：① `toolCalls` 含 `write_file`；② `D:\agent-test\index.html` 真实落盘且是
 *   **可玩贪吃蛇**（含方向键/score/food/重新开始等特征，杜绝「写了空壳」假绿）；
 *   ③ 第二用例验证 `read_file` 回读路径同样可用（同根因类工具，ctx.Get 修复需两端都覆盖）；
 * - 完整运行过程写入证据日志（screenshots/e2e/ai-agent/agent-run-*.log），方便复盘。
 *
 * 运行方式（本机 51888 实例）：
 *   node_modules/.bin/playwright test e2e/plugins/ai-agent/agent-execute.spec.ts \
 *     --config=e2e/plugins/ai-agent/agent-execute.config.ts
 * 可覆盖：E2E_BACKEND_URL（后端地址）、E2E_AGENT_PROJECT_DIR（项目目录）、
 *   E2E_AGENT_MODEL（模型 id）、E2E_API_TOKEN / FORGE_SETTING_CONFIG / FORGE_MACHINE_GUID（鉴权）。
 */
const BACKEND = process.env.E2E_BACKEND_URL ?? 'http://localhost:51888'
const PROJECT_DIR = process.env.E2E_AGENT_PROJECT_DIR ?? 'D:\\agent-test'
const MODEL = process.env.E2E_AGENT_MODEL ?? 'default:google/gemma-4-e4b'

const OUT_DIR = path.resolve(
  fileURLToPath(new URL('../../../screenshots/e2e/ai-agent', import.meta.url)),
)

// 模块加载即解析真实 ApiToken（机器派生密钥解密 51888 ForgeSetting.config）。
const API_TOKEN = resolveHostApiToken()

// 真实贪吃蛇游戏必须出现的特征标记（从 2026-09-11 实测产物归纳，杜绝「写了空壳」假绿）。
const SNAKE_MARKERS = ['keydown', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'score', 'food', '重新开始']

interface RunEvidence {
  projectDir: string
  model: string
  setDirectory: unknown
  chatStatus: number
  chat: unknown
  fileCheck: { existed: boolean; size: number; markers?: string[] } | null
  readToolCalls?: string[]
}

function dumpEvidence(name: string, evidence: RunEvidence): void {
  mkdirSync(OUT_DIR, { recursive: true })
  const file = path.join(OUT_DIR, `${name}.log`)
  const body = [
    `=== AIAgent 端到端执行证据 ===`,
    `backend=${BACKEND}`,
    `projectDir=${evidence.projectDir}`,
    `model=${evidence.model}`,
    `chatStatus=${evidence.chatStatus}`,
    '',
    '--- 设目录响应 ---',
    JSON.stringify(evidence.setDirectory, null, 2),
    '',
    '--- 聊天响应（含 toolCalls）---',
    JSON.stringify(evidence.chat, null, 2),
    '',
    '--- 落盘检查 ---',
    JSON.stringify(evidence.fileCheck, null, 2),
    '',
    '--- read_file 用例 toolCalls ---',
    JSON.stringify(evidence.readToolCalls ?? [], null, 2),
    '',
  ].join('\n')
  writeFileSync(file, body, 'utf8')
  console.log(`\n[evidence] ${file}\n${body}\n`)
}

function assertSnakeContent(indexPath: string): string[] {
  const html = readFileSync(indexPath, 'utf8')
  const lower = html.toLowerCase()
  const missing = SNAKE_MARKERS.filter((m) => !lower.includes(m.toLowerCase()))
  return missing
}

test.describe('AIAgent 端到端执行：真实 LLM 驱动 write_file/read_file 落盘与回读', () => {
  test('设定项目目录 → 建贪吃蛇任务 → agent 调 write_file → index.html 真实落盘且为可玩贪吃蛇', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(180_000)
    const evidence: RunEvidence = {
      projectDir: PROJECT_DIR,
      model: MODEL,
      setDirectory: null,
      chatStatus: 0,
      chat: null,
      fileCheck: null,
    }

    // 0. 清理旧产物：保证「落盘」是本次 agent 真实产出，杜绝陈旧文件导致假绿。
    if (existsSync(PROJECT_DIR)) rmSync(PROJECT_DIR, { recursive: true, force: true })
    mkdirSync(PROJECT_DIR, { recursive: true })

    const auth = { Authorization: `Bearer ${API_TOKEN}` }

    // 1. 设项目目录（文件工具相对此根写盘）
    const dirResp = await request.post(`${BACKEND}/api/project/directory`, {
      headers: auth,
      data: { path: PROJECT_DIR },
    })
    evidence.chatStatus = dirResp.status()
    expect(dirResp.status(), 'POST /api/project/directory 应 200').toBe(200)
    const dirJson = (await dirResp.json()) as { success?: boolean; root?: string; error?: string }
    evidence.setDirectory = dirJson
    expect(dirJson.success, `设目录失败：${JSON.stringify(dirJson)}`).toBe(true)

    // 2. 发送「建贪吃蛇游戏」任务（同步端点，返回完整 toolCalls）
    const task =
      '请在当前项目目录创建一个可玩的贪吃蛇游戏，使用单个 index.html 文件（HTML+CSS+JS 全部内联）。' +
      '必须调用 write_file 工具把游戏代码写入 index.html。' +
      '游戏要求：方向键控制蛇移动、随机生成食物、吃到食物蛇身变长并加分、' +
      '撞墙或撞到自身则游戏结束并显示最终分数、有重新开始按钮。'
    const chatResp = await request.post(`${BACKEND}/api/ai-agent/chat`, {
      headers: { ...auth, 'Content-Type': 'application/json' },
      data: {
        message: task,
        chatModelId: MODEL,
        // 限定工具，既贴近文件类任务、又避免全量工具把小模型 prompt 撑爆导致上游 400
        enabledToolNames: ['write_file', 'read_file', 'list_files'],
      },
    })
    evidence.chatStatus = chatResp.status()
    expect(
      chatResp.status(),
      `POST /api/ai-agent/chat 应 200，实际 ${chatResp.status()}`,
    ).toBe(200)
    const chatJson = (await chatResp.json()) as { toolCalls?: string[]; content?: string }
    evidence.chat = chatJson

    const toolCalls = chatJson.toolCalls ?? []
    console.log(`\n[agent-run] toolCalls=${JSON.stringify(toolCalls)}\n`)

    // 3. 断言 agent 真的调了 write_file（核心能力缺口验证）
    expect(
      toolCalls,
      `agent 未调用任何工具，toolCalls=${JSON.stringify(toolCalls)}；` +
        `最终内容=${JSON.stringify((chatJson.content ?? '').slice(0, 300))}`,
    ).toContain('write_file')

    // 4. 断言文件真实落盘（不是「说了要写」而是「真的写了」）
    const indexPath = path.join(PROJECT_DIR, 'index.html')
    const existed = existsSync(indexPath)
    let size = 0
    let missing: string[] = []
    if (existed) {
      size = statSync(indexPath).size
      missing = assertSnakeContent(indexPath)
    }
    evidence.fileCheck = { existed, size, markers: SNAKE_MARKERS }
    expect(existed, `D:\\agent-test\\index.html 未落盘（agent 未真正写文件）`).toBe(true)
    expect(size, 'index.html 落盘但大小为 0').toBeGreaterThan(0)
    // 5. 内容校验：必须是「可玩贪吃蛇」，而非空壳 HTML
    expect(
      missing,
      `index.html 落盘但非贪吃蛇游戏，缺失特征：${JSON.stringify(missing)}`,
    ).toEqual([])

    dumpEvidence(`agent-run-${Date.now()}`, evidence)
    console.log(`\n[agent-run] ✅ write_file 已触发，index.html 大小=${size}B，贪吃蛇特征齐全\n`)
  })

  test('设定项目目录 → 让 agent 读回 index.html → read_file 路径可用（同根因类工具）', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(180_000)
    const evidence: RunEvidence = {
      projectDir: PROJECT_DIR,
      model: MODEL,
      setDirectory: null,
      chatStatus: 0,
      chat: null,
      fileCheck: null,
      readToolCalls: [],
    }

    const auth = { Authorization: `Bearer ${API_TOKEN}` }

    // 复用第一用例落盘的 index.html：先确保项目目录已设（幂等），且文件存在。
    const dirResp = await request.post(`${BACKEND}/api/project/directory`, {
      headers: auth,
      data: { path: PROJECT_DIR },
    })
    evidence.setDirectory = (await dirResp.json().catch(() => null)) as unknown
    expect(dirResp.status(), 'POST /api/project/directory 应 200').toBe(200)

    const indexPath = path.join(PROJECT_DIR, 'index.html')
    if (!existsSync(indexPath)) {
      // 防御：若第一用例未跑/未落盘，本轮自行建一个最小文件再读，保证用例自包含。
      mkdirSync(PROJECT_DIR, { recursive: true })
      writeFileSync(indexPath, '<!DOCTYPE html><html><body>snake</body></html>', 'utf8')
    }

    // 让 agent 用 read_file 回读 index.html（验证读路径的 ctx.Get 修复同样生效）。
    const readTask =
      '请用 read_file 工具读取当前项目目录下的 index.html，并告诉我这个文件大致在讲什么（例如是否是贪吃蛇游戏）。'
    const chatResp = await request.post(`${BACKEND}/api/ai-agent/chat`, {
      headers: { ...auth, 'Content-Type': 'application/json' },
      data: {
        message: readTask,
        chatModelId: MODEL,
        enabledToolNames: ['write_file', 'read_file', 'list_files'],
      },
    })
    evidence.chatStatus = chatResp.status()
    expect(chatResp.status(), `POST /api/ai-agent/chat 应 200，实际 ${chatResp.status()}`).toBe(200)
    const chatJson = (await chatResp.json()) as { toolCalls?: string[]; content?: string }
    evidence.chat = chatJson
    const toolCalls = chatJson.toolCalls ?? []
    evidence.readToolCalls = toolCalls
    console.log(`\n[agent-read] toolCalls=${JSON.stringify(toolCalls)}\n`)

    expect(
      toolCalls,
      `agent 未调用 read_file，toolCalls=${JSON.stringify(toolCalls)}；` +
        `最终内容=${JSON.stringify((chatJson.content ?? '').slice(0, 300))}`,
    ).toContain('read_file')

    dumpEvidence(`agent-read-${Date.now()}`, evidence)
    console.log(`\n[agent-read] ✅ read_file 已触发，回读路径可用\n`)
  })

  test('方案A（031）：聊天落库后 history 含工具轨迹，刷新可复盘', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(180_000)
    const sessionId = `e2e-tooltrace-${Date.now()}`
    const auth = { Authorization: `Bearer ${API_TOKEN}` }

    // 1. 设项目目录（幂等）
    const dirResp = await request.post(`${BACKEND}/api/project/directory`, {
      headers: auth,
      data: { path: PROJECT_DIR },
    })
    expect(dirResp.status(), 'POST /api/project/directory 应 200').toBe(200)

    // 2. 发带工具的聊天（指定 sessionId，便于回查 history）
    const task =
      '请在当前项目目录创建 hello.txt，内容为 "tooltrace ok"，必须调用 write_file 工具写入。'
    const chatResp = await request.post(`${BACKEND}/api/ai-agent/chat`, {
      headers: { ...auth, 'Content-Type': 'application/json' },
      data: {
        message: task,
        chatModelId: MODEL,
        sessionId,
        enabledToolNames: ['write_file', 'read_file', 'list_files'],
      },
    })
    expect(
      chatResp.status(),
      `POST /api/ai-agent/chat 应 200，实际 ${chatResp.status()}`,
    ).toBe(200)
    const chatJson = (await chatResp.json()) as { toolCalls?: string[]; toolCallsJson?: string }
    const toolCalls = chatJson.toolCalls ?? []
    console.log(`\n[agent-trace] 即时 toolCalls=${JSON.stringify(toolCalls)}\n`)
    expect(
      toolCalls,
      `agent 未调用工具，toolCalls=${JSON.stringify(toolCalls)}；content=${(chatJson as { content?: string }).content ?? ''}`,
    ).toContain('write_file')

    // 3. 回查 history（模拟刷新后从 DB 复盘）—— 验证轨迹已随 assistant 消息落库
    const histResp = await request.get(`${BACKEND}/api/ai-agent/chat/history/${sessionId}`)
    expect(histResp.status(), `GET history 应 200，实际 ${histResp.status()}`).toBe(200)
    const hist = (await histResp.json()) as Array<{ toolCallsJson?: string }>
    console.log(`\n[agent-trace] history 条数=${hist.length}\n`)

    const traced = hist.find((m) => m.toolCallsJson && m.toolCallsJson.length > 2)
    expect(
      traced,
      `history 中找不到含 toolCallsJson 的消息；history=${JSON.stringify(hist)}`,
    ).toBeDefined()

    let traces: Array<{ name?: string; result?: string; success?: boolean }> = []
    try {
      traces = JSON.parse(traced!.toolCallsJson!) as Array<{
        name?: string
        result?: string
        success?: boolean
      }>
    } catch (e) {
      throw new Error(`toolCallsJson 非合法 JSON：${traced!.toolCallsJson}；err=${String(e)}`)
    }
    console.log(`\n[agent-trace] 落库轨迹=${JSON.stringify(traces)}\n`)
    expect(
      traces.some((t) => t.name === 'write_file' && t.success === true && !!t.result),
      `落库轨迹未含「write_file 成功且带结果」；traces=${JSON.stringify(traces)}`,
    ).toBe(true)

    console.log(`\n[agent-trace] ✅ 方案A 验证通过：history 含 write_file 工具轨迹（刷新可复盘）\n`)
  })
})
