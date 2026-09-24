import { afterEach, describe, expect, it, vi } from 'vitest'
import { streamAgentChat, type AgentStreamHandlers } from './http'

/**
 * streamAgentChat（POST /api/ai-agent/chat/stream）SSE 事件契约单测。
 *
 * 后端真实事件集（2026-09-24 实证，AIChatController.cs:193-230）：
 *   content / tool_call / tool_result / usage / done / error
 * done 事件字段 = { type, sessionId, responseId, usage, toolCallsJson }。
 *
 * ⚠ 曾存在 4 条「自主循环」超前契约用例（turn 事件 + done.stopReason/turns/maxTurns）：
 * 描述的后端行为从未实现——全后端无 turn/stopReason/maxTurns（grep 实证）。
 * 补前端透传只会造出永不触发的死代码，故 2026-09-24 改写为与真实后端一致。
 * 若未来实现「自治多轮」（后端下发 turn 事件 + done 携带 stopReason/turns/maxTurns），
 * 须同时恢复下述用例 1/2 的原契约，并同步 UI 消费。
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
    onUsage: (u) => calls.push(['usage', u]),
    onDone: (e) => calls.push(['done', e]),
    onError: (m) => calls.push(['error', m]),
  }
  return { calls, handlers }
}

const PAYLOAD = { sessionId: 's1', message: 'hi' }

describe('streamAgentChat — SSE 事件契约（对齐真实后端）', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('未知事件类型被安全忽略，不中断后续事件流（后端新增事件的守卫）', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(
          sseBody([
            { type: 'content', content: 'a' },
            // 未来可能新增的事件（如自治多轮的 turn）——当前后端不下发，前端必须静默跳过
            { type: 'turn', content: '第 1/20 轮结束', turns: 1, maxTurns: 20 },
            { type: 'content', content: 'b' },
            { type: 'done', sessionId: 's1', responseId: 7 },
          ]),
        ),
      ),
    )

    await streamAgentChat(PAYLOAD, handlers)

    expect(calls.filter(([k]) => k === 'content').map(([, v]) => v)).toEqual(['a', 'b'])
    expect(calls.filter(([k]) => k === 'turn')).toEqual([])
    expect((calls.find(([k]) => k === 'done')?.[1] as { sessionId?: string }).sessionId).toBe('s1')
  })

  it('done 事件透出后端真实字段 sessionId/responseId/usage', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(
          sseBody([
            {
              type: 'done',
              sessionId: 's1',
              responseId: 42,
              usage: { promptTokens: 10, completionTokens: 20, totalTokens: 30 },
            },
          ]),
        ),
      ),
    )

    await streamAgentChat(PAYLOAD, handlers)

    const done = calls.find(([k]) => k === 'done')?.[1] as {
      sessionId?: string
      responseId?: number
      usage?: { totalTokens?: number }
    }
    expect(done.sessionId).toBe('s1')
    expect(done.responseId).toBe(42)
    expect(done.usage?.totalTokens).toBe(30)
  })

  it('done 事件缺 usage 字段时透出 undefined，不崩溃', async () => {
    const { calls, handlers } = makeHandlers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(sseBody([{ type: 'done', sessionId: 's1', responseId: 1 }])),
      ),
    )

    await streamAgentChat(PAYLOAD, handlers)

    const done = calls.find(([k]) => k === 'done')?.[1] as { usage?: unknown }
    expect(done.usage).toBeUndefined()
  })

  it('事件跨 chunk 拆分时仍能正确解析（SSE 按 \\n\\n 分帧）', async () => {
    const { calls, handlers } = makeHandlers()
    const body = sseBody([
      { type: 'content', content: 'hello' },
      { type: 'done', sessionId: 's1', responseId: 9 },
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
    expect((calls.find(([k]) => k === 'done')?.[1] as { sessionId?: string }).sessionId).toBe('s1')
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