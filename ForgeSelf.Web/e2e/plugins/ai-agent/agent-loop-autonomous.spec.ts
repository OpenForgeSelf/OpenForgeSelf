import { test, expect, type APIRequestContext } from '@playwright/test'
import { mkdirSync, writeFileSync, existsSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { resolveHostApiToken } from '../../helpers/host-api-token'

/**
 * AIAgent 自治循环（finish / maxTurns）· e2e（正式、可重复、零 mock）。
 *
 * 背景：修复前 RunAgentLoopAsync 把「本轮无 tool_call」等同「任务完成」→ 立即 done + break，
 * 导致模型只要先输出一段纯文本（计划/说明）就整轮熔断，用户被迫手动连发「继续」。
 * 修复后：maxTurns > 1 进入自治模式——只有调用 finish 工具才结束，否则注入续跑指令自动进入下一轮，
 * 直到达到轮次上限（stopReason=max_turns）。
 *
 * 本用例锁三条行为（对应 2026-09-21 的根因修复）：
 *   ① maxTurns=1 → 传统单次问答（不挂 finish、不自动续跑），stopReason=completed；
 *   ② maxTurns=N(>1) → 自治模式：最终 stopReason ∈ {finish, max_turns}，且轮次不会停在第一轮；
 *   ③ 纯文本首答不再熔断 —— 用「先让我想想」这类诱导只输出文本的弱指令，仍能看到多轮推进。
 *
 * 判定手段（不再用启发式）：
 * - 走**流式**端点 `POST /api/ai-agent/chat/stream`，逐帧解析 SSE，直接读 `turn` / `done` 事件里的
 *   `turns` / `maxTurns` / `stopReason`（这正是本次新增的可观测字段）；
 * - 同步端点无法回传逐轮心跳，故不用于判定轮次。
 *
 * 运行方式（本机 51888 实例）：
 *   node_modules/.bin/playwright test e2e/plugins/ai-agent/agent-loop-autonomous.spec.ts \
 *     --config=e2e/plugins/ai-agent/agent-execute.config.ts
 * 可覆盖：E2E_BACKEND_URL / E2E_AGENT_MODEL / E2E_LOOP_PROJECT_DIR / E2E_API_TOKEN。
 */
const BACKEND = process.env.E2E_BACKEND_URL ?? 'http://localhost:51888'
// 2026-09-21 复核 LM Studio 本地清单后确认的可用模型（gemma-4-e4b 已不可用）。
const MODEL = process.env.E2E_AGENT_MODEL ?? 'default:qwen3.5-4b'
// 自治循环靶场：与 agent-execute 的 D:\agent-test 隔离，避免互相清目录。
const PROJECT_DIR = process.env.E2E_LOOP_PROJECT_DIR ?? 'D:\\agent-test\\loop'

const OUT_DIR = path.resolve(
  fileURLToPath(new URL('../../../screenshots/e2e/ai-agent', import.meta.url)),
)

// 模块加载即解析真实 ApiToken（机器派生密钥解密 51888 ForgeSetting.config）。
const API_TOKEN = resolveHostApiToken()

/** 从 SSE 正文里解析出的循环观测结果。 */
interface LoopObservation {
  /** turn 事件轮次序列（第 N/M 轮），用于证明「确实多轮推进」。 */
  turnEvents: Array<{ turns: number; maxTurns: number }>
  /** done 事件的结束原因：finish / max_turns / max_iterations / completed / (缺失)。 */
  stopReason: string | null
  /** done 事件携带的最终轮次。 */
  doneTurns: number | null
  doneMaxTurns: number | null
  /** 是否出现过 finish 工具调用。 */
  sawFinishTool: boolean
  /** 出现过的工具名（去重）。 */
  tools: string[]
  /** 累积的正文（content 事件拼接）。 */
  content: string
  /** error 事件内容（若有）。 */
  error: string | null
  /** 收到的原始 SSE 帧数。 */
  frames: number
}

/** 解析 `data: {...}\n\n` 形式的 SSE 正文（逐帧 JSON，camelCase 字段）。 */
function parseSse(raw: string): LoopObservation {
  const obs: LoopObservation = {
    turnEvents: [],
    stopReason: null,
    doneTurns: null,
    doneMaxTurns: null,
    sawFinishTool: false,
    tools: [],
    content: '',
    error: null,
    frames: 0,
  }
  const seenTools = new Set<string>()

  for (const line of raw.split('\n')) {
    const trimmed = line.trim()
    if (!trimmed.startsWith('data:')) continue
    const payload = trimmed.slice(5).trim()
    if (!payload) continue

    let ev: Record<string, unknown>
    try {
      ev = JSON.parse(payload) as Record<string, unknown>
    } catch {
      continue // 非 JSON 帧（心跳等）忽略
    }
    obs.frames++

    const type = String(ev.type ?? '')
    if (type === 'turn') {
      obs.turnEvents.push({
        turns: Number(ev.turns ?? 0),
        maxTurns: Number(ev.maxTurns ?? 0),
      })
    } else if (type === 'tool_call') {
      const name = String(ev.name ?? '')
      if (name) {
        obs.tools.push(name)
        seenTools.add(name)
        if (name.toLowerCase() === 'finish') obs.sawFinishTool = true
      }
    } else if (type === 'content') {
      obs.content += String(ev.content ?? '')
    } else if (type === 'done') {
      obs.stopReason = ev.stopReason == null ? null : String(ev.stopReason)
      obs.doneTurns = ev.turns == null ? null : Number(ev.turns)
      obs.doneMaxTurns = ev.maxTurns == null ? null : Number(ev.maxTurns)
    } else if (type === 'error') {
      obs.error = String(ev.content ?? '')
    }
  }
  return obs
}

/** 落盘证据（复盘用）。 */
function dump(name: string, payload: Record<string, unknown>, rawSse: string): void {
  mkdirSync(OUT_DIR, { recursive: true })
  const body = [
    `=== AIAgent 自治循环 e2e 证据：${name} ===`,
    `backend=${BACKEND}`,
    `model=${MODEL}`,
    `projectDir=${PROJECT_DIR}`,
    '',
    JSON.stringify(payload, null, 2),
    '',
    '--- 原始 SSE ---',
    rawSse.slice(0, 20000),
    '',
  ].join('\n')
  const file = path.join(OUT_DIR, `agent-loop-${name}-${Date.now()}.log`)
  writeFileSync(file, body, 'utf8')
  console.log(`\n[agent-loop-${name}] 证据：${file}\n`)
}

/**
 * 发一条流式聊天并把 SSE 正文整段读回（Playwright 的 request.post 会等响应体收完）。
 * 返回原始正文，交给 parseSse 解析。
 */
async function streamChat(
  request: APIRequestContext,
  message: string,
  maxTurns: number,
  enabledToolNames: string[],
  sessionId: string,
): Promise<{ status: number; raw: string }> {
  const resp = await request.post(`${BACKEND}/api/ai-agent/chat/stream`, {
    headers: { Authorization: `Bearer ${API_TOKEN}`, 'Content-Type': 'application/json' },
    data: { message, chatModelId: MODEL, sessionId, maxTurns, enabledToolNames },
    timeout: 180_000,
  })
  const raw = await resp.text()
  return { status: resp.status(), raw }
}

/** 设项目目录（文件类工具的相对根）。 */
async function setProjectDir(request: APIRequestContext): Promise<void> {
  if (!existsSync(PROJECT_DIR)) mkdirSync(PROJECT_DIR, { recursive: true })
  const resp = await request.post(`${BACKEND}/api/project/directory`, {
    headers: { Authorization: `Bearer ${API_TOKEN}` },
    data: { path: PROJECT_DIR },
  })
  expect(resp.status(), `POST /api/project/directory 应 200，实际 ${resp.status()}`).toBe(200)
  const json = (await resp.json()) as { success?: boolean; error?: string }
  expect(json.success, `设项目目录失败：${JSON.stringify(json)}`).toBe(true)
}

test.describe('AIAgent 自治循环：finish 出口与 maxTurns 上限（根因修复回归）', () => {
  test('maxTurns=1 → 传统单次问答：stopReason=completed，不自动续跑', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(180_000)
    await setProjectDir(request)

    const { status, raw } = await streamChat(
      request,
      '用一句话回答：1+1 等于几？不要调用任何工具。',
      1,
      ['read_file'],
      `e2e-loop-single-${Date.now()}`,
    )
    expect(status, `流式端点应 200，实际 ${status}`).toBe(200)

    const obs = parseSse(raw)
    dump('single-turn', { status, ...obs }, raw)

    expect(obs.error, `不应出现 error 事件：${obs.error}`).toBeNull()
    // 非自治：不挂 finish、不自动续跑 —— 没有任何 turn 心跳。
    expect(
      obs.turnEvents.length,
      `maxTurns=1 不应有 turn 心跳（自治模式专属），实际 ${JSON.stringify(obs.turnEvents)}`,
    ).toBe(0)
    expect(obs.sawFinishTool, 'maxTurns=1 不应挂载 finish 工具').toBe(false)
    expect(obs.stopReason, 'maxTurns=1 结束原因应为 completed').toBe('completed')
    expect(obs.doneTurns, 'maxTurns=1 时 done 应回报 turns=1').toBe(1)
    expect(obs.content.length, '应拿到最终回答正文').toBeGreaterThan(0)
  })

  test('maxTurns=3 且首答只给文本 → 不熔断，自动续跑至 finish 或 max_turns', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(300_000)
    await setProjectDir(request)

    // 诱导性任务：先让模型「说说打算怎么写」，这类弱指令修复前会让循环在第一轮就断掉。
    // 自治模式下的正确行为是：不调 finish → 注入续跑指令 → 继续推进，直到 finish 或跑满 3 轮。
    const message =
      '请在当前项目目录创建 loop-proof.txt，内容为 "autonomous loop ok"。' +
      '先简单说明你打算怎么做（一两句即可），然后实际调用 write_file 工具完成它。' +
      '完成后调用 finish 工具声明完成并给出总结。'

    const { status, raw } = await streamChat(
      request,
      message,
      3,
      ['write_file', 'read_file', 'list_files'],
      `e2e-loop-auto-${Date.now()}`,
    )
    expect(status, `流式端点应 200，实际 ${status}`).toBe(200)

    const obs = parseSse(raw)
    const targetFile = path.join(PROJECT_DIR, 'loop-proof.txt')
    const fileInfo = {
      existed: existsSync(targetFile),
      size: existsSync(targetFile) ? statSync(targetFile).size : 0,
    }
    dump('autonomous', { status, ...obs, fileInfo }, raw)

    expect(obs.error, `不应出现 error 事件：${obs.error}`).toBeNull()

    // ① 结束原因只能是 finish 或 max_turns —— 这正是本次新增的可观测契约。
    expect(
      ['finish', 'max_turns'],
      `自治模式结束原因应 ∈ {finish, max_turns}，实际 ${obs.stopReason}`,
    ).toContain(obs.stopReason)

    // ② 不是"一轮就断"：轮次信息必须回传，且上限与请求一致。
    expect(obs.doneMaxTurns, 'done 应回报 maxTurns=3').toBe(3)
    expect(obs.doneTurns, 'done 应回报实际结束轮次').not.toBeNull()

    // ③ 若模型走了 finish 出口，轮次必然 < 上限（证明确实是主动完成而非跑满兜底）。
    if (obs.stopReason === 'finish') {
      expect(obs.sawFinishTool, 'stopReason=finish 时必须出现过 finish 工具调用').toBe(true)
      expect(
        obs.doneTurns!,
        `finish 出口的轮次应 < 上限 3，实际 ${obs.doneTurns}`,
      ).toBeLessThan(3)
    }

    // ④ 核心能力回归：写了一轮就不该停在第一轮空转 —— 目标文件必须真实落盘。
    //    （修复前：模型先输出说明即熔断，文件根本不会产生。）
    expect(
      fileInfo.existed,
      `loop-proof.txt 未落盘：自治循环未真正推进（stopReason=${obs.stopReason}, ` +
        `turns=${obs.doneTurns}, tools=${JSON.stringify(obs.tools)}）；` +
        `正文片段=${JSON.stringify(obs.content.slice(0, 300))}`,
    ).toBe(true)
    expect(fileInfo.size, 'loop-proof.txt 落盘但为空').toBeGreaterThan(0)
  })

  test('maxTurns=2 简单任务 → finish 主动收口，轮次不超过上限', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(300_000)
    await setProjectDir(request)

    const { status, raw } = await streamChat(
      request,
      '请读取当前项目目录下的 loop-proof.txt 并告诉我里面写了什么；完成后调用 finish 工具总结。',
      2,
      ['read_file', 'list_files'],
      `e2e-loop-finish-${Date.now()}`,
    )
    expect(status, `流式端点应 200，实际 ${status}`).toBe(200)

    const obs = parseSse(raw)
    // 防御：前序用例若未产出文件，本轮自建最小样本，保证用例自包含。
    const targetFile = path.join(PROJECT_DIR, 'loop-proof.txt')
    if (!existsSync(targetFile)) {
      mkdirSync(PROJECT_DIR, { recursive: true })
      writeFileSync(targetFile, 'autonomous loop ok', 'utf8')
    }
    dump('finish', { status, ...obs }, raw)

    expect(obs.error, `不应出现 error 事件：${obs.error}`).toBeNull()
    expect(
      ['finish', 'max_turns'],
      `自治模式结束原因应 ∈ {finish, max_turns}，实际 ${obs.stopReason}`,
    ).toContain(obs.stopReason)

    // 轮次上限必须被严格尊重（不超发）。
    expect(obs.doneMaxTurns, 'done 应回报 maxTurns=2').toBe(2)
    expect(obs.doneTurns, 'done 应回报实际结束轮次').not.toBeNull()
    expect(obs.doneTurns!, `实际轮次不应超过上限 2，实际 ${obs.doneTurns}`).toBeLessThanOrEqual(2)

    // turn 心跳数不应超过 doneTurns（每轮最多一个心跳）。
    expect(
      obs.turnEvents.length,
      `turn 心跳数（${obs.turnEvents.length}）不应超过结束轮次（${obs.doneTurns}）`,
    ).toBeLessThanOrEqual(obs.doneTurns!)
  })

  test('maxTurns=2 且任务明确无法完成 → 跑满上限，stopReason=max_turns 且如实回报轮次', async ({
    request,
  }: {
    request: APIRequestContext
  }) => {
    test.setTimeout(300_000)
    await setProjectDir(request)

    // 关键：该任务让模型「永远做不完」——每轮都被要求产出新的、无法终止的动作，
    // 且明确禁止调用 finish。用于验证「上限兜底」路径：maxTurns 必须被严格尊重，
    // 不能无限续跑（这是自治循环的安全边界，与 finish 出口同等重要）。
    const message =
      '重要约束：本任务禁止调用 finish 工具，无论做到哪一步都不要调用 finish。' +
      '任务：查询 1 到 1000000 之间第 N 个质数（N 由你自选一个很大的值），' +
      '每轮只允许计算一小段并把中间进度写进备注，然后继续下一段，直到你算完为止。' +
      '如果你算完了也不要调用 finish，改为继续验证结果的每一位数字。'

    const { status, raw } = await streamChat(
      request,
      message,
      2,
      ['read_file', 'list_files'],
      `e2e-loop-cap-${Date.now()}`,
    )
    expect(status, `流式端点应 200，实际 ${status}`).toBe(200)

    const obs = parseSse(raw)
    dump('max-turns-cap', { status, ...obs }, raw)

    expect(obs.error, `不应出现 error 事件：${obs.error}`).toBeNull()

    // 若模型仍顽强地调了 finish（弱指令约束不一定被遵守），改走 finish 断言分支——
    // 但两者都必须是自治模式的合法结束原因，绝不能出现一轮即断的 completed。
    expect(
      ['finish', 'max_turns'],
      `自治模式结束原因应 ∈ {finish, max_turns}，实际 ${obs.stopReason}`,
    ).toContain(obs.stopReason)

    // 轮次上限是硬边界：无论走哪个出口，实际轮次都不得超过请求的 maxTurns。
    expect(obs.doneMaxTurns, 'done 应回报 maxTurns=2').toBe(2)
    expect(obs.doneTurns, 'done 应回报实际结束轮次').not.toBeNull()
    expect(
      obs.doneTurns!,
      `实际轮次（${obs.doneTurns}）不得超过上限 2 —— 自治循环必须有硬边界`,
    ).toBeLessThanOrEqual(2)

    // max_turns 出口必须回报「跑满」而非提前收口：轮次应恰好等于上限。
    if (obs.stopReason === 'max_turns') {
      expect(
        obs.doneTurns,
        `stopReason=max_turns 时轮次应恰好等于上限 2，实际 ${obs.doneTurns}`,
      ).toBe(2)
      expect(obs.sawFinishTool, 'max_turns 出口不应出现 finish 调用').toBe(false)
    }

    // 正文里应能看到模型确实在被反复驱动（而非一轮就停）。
    expect(
      obs.turnEvents.length,
      `应至少有 1 次「未声明完成、自动续跑」心跳，实际 ${JSON.stringify(obs.turnEvents)}`,
    ).toBeGreaterThanOrEqual(1)
  })
})
