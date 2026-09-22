import { afterEach, describe, expect, it, vi } from 'vitest'
import { streamAgentChat, type AgentStreamHandlers } from './http'

/**
 * 自主循环（agent loop 自动续跑）前端契约单测。
 *
 * 后端在自治模式下会下发两类新事件，前端必须正确识别，否则：
 * - `turn` 被忽略 → 长任务期间界面看不到「第 N/M 轮」，看起来像卡住；
 * - `done.stopReason` 被忽略 → 达到轮次上限（任务未完成）时会被误读为「已完成」。
 *
 * 这里用假 ReadableStream 构造 SSE 响应，逐事件断言回调被正确触发（零真实网络）。
 */

/** 把若干 SSE 事件串成后端真实格式（`data: {json}\n\n`）。 */
function sseBody(events: unknown[]): string {
  return events.map((e) => `data: ${JSON.stringify(e)}\n\n`).join('')
}

/** 构造一个只吐固定 SSE 文本的假 Response。 */
function fakeResponse(body: string): Response {
  const bytes = new TextEncoder().encode(body)
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(bytes)
      controller.close()
    },
  })
  return {
    ok: true,
    status: 200,
    body: stream,
    json: async () => ({}),
  } as unknown as Response
}

/** 记录所有回调调用的探针。 */
function makeHandlers() {
  const calls: Array<[string, unknown]> = []
  const handlers: AgentStreamHandlers = {
    onContent: (c) => calls.push(['content', c]),
    onToolCall: (e) => calls.push(['tool_call', e]),
    onToolResult: (e) => calls.push(['tool_result', e]),
    onTurn: (e) => calls.push(['turn', e]),
    onUsage: (u) => calls.push(['usage', u]),
    onDone: (e) => calls.push(['done', e]),
    onError: (m) => calls.push(['error', m]),
  }
  return { calls, handlers }
}

const PAYLOAD = { sessionId: 's1', message: 'hi' }

describe('streamAgentChat — 自主循环事件契约', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('turn 事件携带 turns/maxTurns，驱动前端「第 N/M 轮」进度', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(
          sseBody([
            { type: 'content', content: 'a' },
            { type: 'turn', content: '第 1/20 轮结束', turns: 1, maxTurns: 20 },
            { type: 'content', content: 'b' },
            { type: 'turn', content: '第 2/20 轮结束', turns: 2, maxTurns: 20 },
            { type: 'done', sessionId: 's1', stopReason: 'finish', turns: 2, maxTurns: 20 },
          ]),
        ),
      ),
    )

    await streamAgentChat(PAYLOAD, handlers)

    const turns = calls.filter(([k]) => k === 'turn').map(([, v]) => v)
    expect(turns).toEqual([
      { turns: 1, maxTurns: 20, content: '第 1/20 轮结束' },
      { turns: 2, maxTurns: 20, content: '第 2/20 轮结束' },
    ])
  })

  it('done 事件透出 stopReason 与实际轮次（用于区分「模型声明完成」与「触上限」）', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(
          sseBody([
            { type: 'turn', turns: 1, maxTurns: 3, content: '第 1/3 轮结束' },
            { type: 'done', sessionId: 's1', responseId: 42, stopReason: 'max_turns', turns: 3, maxTurns: 3 },
          ]),
        ),
      ),
    )

    await streamAgentChat(PAYLOAD, handlers)

    const done = calls.find(([k]) => k === 'done')?.[1] as {
      stopReason?: string
      turns?: number
      maxTurns?: number
    }
    expect(done.stopReason).toBe('max_turns')
    expect(done.turns).toBe(3)
    expect(done.maxTurns).toBe(3)
  })

  it('finish 结束原因原样透传（模型调用完成工具的正常收尾）', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(
          sseBody([{ type: 'done', sessionId: 's1', stopReason: 'finish', turns: 4, maxTurns: 20 }]),
        ),
      ),
    )

    await streamAgentChat(PAYLOAD, handlers)

    const done = calls.find(([k]) => k === 'done')?.[1] as { stopReason?: string }
    expect(done.stopReason).toBe('finish')
  })

  it('事件跨 chunk 拆分时仍能正确解析（SSE 按 \\n\\n 分帧）', async () => {
    const { calls, handlers } = makeHandlers()
    const body = sseBody([
      { type: 'content', content: 'hello' },
      { type: 'done', sessionId: 's1', stopReason: 'completed' },
    ])
    // 故意在事件中间切断，模拟 TCP 分片
    const cut = Math.floor(body.length / 2)
    const enc = new TextEncoder()
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(enc.encode(body.slice(0, cut)))
        controller.enqueue(enc.encode(body.slice(cut)))
        controller.close()
      },
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({ ok: true, status: 200, body: stream, json: async () => ({}) }) as unknown as Response),
    )

    await streamAgentChat(PAYLOAD, handlers)

    expect(calls.filter(([k]) => k === 'content').map(([, v]) => v)).toEqual(['hello'])
    expect((calls.find(([k]) => k === 'done')?.[1] as { stopReason?: string }).stopReason).toBe('completed')
  })

  it('error 事件透出错误文案', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => fakeResponse(sseBody([{ type: 'error', content: '上游 500' }]))),
    )

    await streamAgentChat(PAYLOAD, handlers)

    expect(calls.find(([k]) => k === 'error')?.[1]).toBe('上游 500')
  })
})
