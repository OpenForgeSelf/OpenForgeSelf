import { afterEach, describe, expect, it, vi } from 'vitest'
import { buildAgentChatConfig, type AgentBridgeDeps } from './agentBridge'
import { useChat } from './agent-core/composables/useChat'

/**
 * v1.8.0 迁移（ai-assistant-widget → AIAgent 插件）的适配层单测。
 *
 * 覆盖三块：
 * 1. buildAgentChatConfig 的 config 形状（内嵌形态 / 隐藏组件标题栏 / 关闭组件持久化）；
 * 2. transport 分流契约：free → POST /api/ai-agent/chat/stream（增量 onDelta），
 *    plan → POST /api/ai-agent/runs（终局 onDone(content) 喂 onDelta）；
 * 3. useChat 的 transport 集成（消息收尾 / lastUsage / 错误条 / hydrate）。
 *
 * SSE 假流范式对齐 http.test.ts：vi.stubGlobal fetch + 假 ReadableStream，零真实网络。
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

/** 默认 deps：free 模式 Agent、已选模型、无工具/技能。 */
function makeDeps(overrides: Partial<AgentBridgeDeps> = {}): AgentBridgeDeps {
  return {
    sessionId: () => 'sess-1',
    activeAgentId: () => 'agent-1',
    selectedModelId: () => 'model-1',
    selectedToolNames: () => [],
    selectedSkillIds: () => [],
    activeAgent: () => ({ id: 'agent-1', name: 'A1', executionMode: 'free' } as never),
    ...overrides,
  }
}

/** 记录一次 transport 调用的探针。 */
function makeCtx() {
  const calls: Array<[string, unknown]> = []
  return {
    calls,
    ctx: {
      text: '你好',
      sessionId: 'sess-1',
      model: 'model-1',
      systemPrompt: '',
      onDelta: (c: string) => calls.push(['delta', c]),
      onUsage: (u: unknown) => calls.push(['usage', u]),
      onError: (e: Error) => {
        throw e
      },
    },
  }
}

describe('buildAgentChatConfig — 内嵌形态配置', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('返回 AIAgent 内嵌形态：隐藏组件标题栏、常驻打开、关闭组件持久化/历史', () => {
    const cfg = buildAgentChatConfig(makeDeps())
    expect(cfg.showHeader).toBe(false)
    expect(cfg.inline).toBe(true)
    expect(cfg.persist).toBe(false)
    expect(cfg.history).toMatchObject({ enabled: false })
    expect(cfg.autoOpen).toBe(true)
    expect(typeof cfg.transport).toBe('function')
  })

  it('free 分流：走 /api/ai-agent/chat/stream，增量 onDelta 逐块收到，usage 上抛，工具事件忽略', async () => {
    const { calls, ctx } = makeCtx()
    const fetchMock = vi.fn(async (_url: unknown, init?: RequestInit) => {
      // 断言请求体：会话/消息/模型/Agent 透传；未选工具技能时不下发
      const body = JSON.parse(String(init?.body))
      expect(body.sessionId).toBe('sess-1')
      expect(body.message).toBe('你好')
      expect(body.chatModelId).toBe('model-1')
      expect(body.agentId).toBe('agent-1')
      expect(body.enabledToolNames).toBeUndefined()
      expect(body.skillIds).toBeUndefined()
      return fakeResponse(
        sseBody([
          { type: 'content', content: '你' },
          { type: 'tool_call', name: 'search', arguments: '{}' },
          { type: 'tool_result', name: 'search', result: '[]', success: true },
          { type: 'content', content: '好' },
          { type: 'usage', usage: { promptTokens: 10, completionTokens: 5, totalTokens: 15 } },
          { type: 'done', sessionId: 'sess-1', responseId: 7 },
        ]),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const cfg = buildAgentChatConfig(makeDeps())
    await (cfg.transport as NonNullable<typeof cfg.transport>)(ctx as never)

    expect(fetchMock).toHaveBeenCalledWith('/api/ai-agent/chat/stream', expect.anything())
    // 增量逐块 + 终局内容完整（忽略工具事件）
    expect(calls.filter(([k]) => k === 'delta').map(([, v]) => v)).toEqual(['你', '好'])
    // usage 上抛
    expect(calls.find(([k]) => k === 'usage')?.[1]).toMatchObject({ totalTokens: 15 })
  })

  it('plan 分流：走 /api/ai-agent/runs，步骤增量忽略、终局交付文本喂 onDelta', async () => {
    const { calls, ctx } = makeCtx()
    const fetchMock = vi.fn(async (_url: unknown, init?: RequestInit) => {
      const body = JSON.parse(String(init?.body))
      expect(body.sessionId).toBe('sess-1')
      expect(body.agentId).toBe('agent-1')
      expect(body.taskInput).toBe('你好')
      expect(body.chatModelId).toBe('model-1')
      return fakeResponse(
        sseBody([
          { type: 'plan_created', payload: JSON.stringify({ title: 'P1', steps: [] }) },
          { type: 'step_started', payload: JSON.stringify({ stepIndex: 0 }) },
          { type: 'content', content: '步骤中间产出（本批不展示）' },
          { type: 'step_completed', payload: JSON.stringify({ stepIndex: 0 }) },
          { type: 'done', payload: '这是最终交付文本' },
        ]),
      )
    })
    vi.stubGlobal('fetch', fetchMock)

    const cfg = buildAgentChatConfig(
      makeDeps({
        activeAgent: () => ({ id: 'agent-1', name: 'A1', executionMode: 'plan' } as never),
      }),
    )
    await (cfg.transport as NonNullable<typeof cfg.transport>)(ctx as never)

    expect(fetchMock).toHaveBeenCalledWith('/api/ai-agent/runs', expect.anything())
    // 只有终局交付文本进 delta；步骤中间产出被忽略
    expect(calls.filter(([k]) => k === 'delta').map(([, v]) => v)).toEqual(['这是最终交付文本'])
  })

  it('错误事件：transport 内转为 Error 抛给 ctx.onError', async () => {
    const { ctx } = makeCtx()
    let thrown: Error | null = null
    ctx.onError = (e: Error) => {
      thrown = e
    }
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        fakeResponse(sseBody([{ type: 'error', content: '模型不可用' }])),
      ),
    )

    const cfg = buildAgentChatConfig(makeDeps())
    // streamAgentChat 对 error 事件调 onError（不 throw），transport 内 onError 抛 → ctx.onError 被调
    await (cfg.transport as NonNullable<typeof cfg.transport>)(ctx as never)

    expect(thrown?.message).toContain('模型不可用')
  })
})

describe('useChat — transport 集成（v1.8.0）', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('transport 路径：用户消息 + 流式增量收尾成 assistant 消息，lastUsage 填充', async () => {
    const chat = useChat({
      persist: false,
      history: { enabled: false },
      transport: async (t: {
        onDelta: (c: string) => void
        onUsage: (u: unknown) => void
      }) => {
        t.onDelta('流')
        t.onDelta('式回复')
        t.onUsage({ promptTokens: 3, completionTokens: 2, totalTokens: 5 })
      },
    } as never)

    await chat.send('你好')

    const roles = chat.messages.value.map((m: { role: string }) => m.role)
    expect(roles).toEqual(['user', 'assistant'])
    expect(chat.messages.value[1].content).toBe('流式回复')
    expect(chat.lastUsage.value).toMatchObject({ totalTokens: 5 })
    expect(chat.sending.value).toBe(false)
  })

  /* 这条用例 2026-10-16 被**有意改了断言**（原：失败时一条 assistant 消息都不留）。
     改动原因：状态矩阵里「生成失败」这一态要有实体，否则 UI 上那套
     失败样式 + 重试按钮永远没有数据源，等于一段死代码 —— 比不做更糟。
     但原用例的**真实意图被保留下来了**：它防的是「留一条空白占位」，
     所以新断言不是「留了就行」，而是「留的必须带 status:'failed' 与失败原因」——
     一条没有状态标记的空白消息依旧是不合格的，仍会被这里拦下。 */
  it('transport 错误：置 error 条，并留一条带状态标记的痕迹（不是空白占位）', async () => {
    const chat = useChat({
      persist: false,
      history: { enabled: false },
      fallbackToMock: false,
      transport: async () => {
        throw new Error('上游不可用')
      },
    } as never)

    await chat.send('你好')

    expect(chat.error.value).toBe('上游不可用')
    expect(chat.messages.value.length).toBe(2)
    const m = chat.messages.value[1]
    expect(m.role).toBe('assistant')
    // 留痕的前提是看得出为什么失败：没有这两样的那条就是空气泡，不如不留
    expect(m.status).toBe('failed')
    expect(m.error).toBe('上游不可用')
  })

  it('hydrate：会话切换灌入历史，替换消息列表并清空流式/错误状态', () => {
    const chat = useChat({ persist: false, history: { enabled: false } } as never)
    chat.hydrate([
      { id: 'h1', role: 'user', content: '早' },
      { id: 'h2', role: 'assistant', content: '你好' },
    ])
    expect(chat.messages.value.length).toBe(2)
    expect(chat.messages.value[1].content).toBe('你好')

    // 再次 hydrate 覆盖（切换会话）
    chat.hydrate([{ id: 'h3', role: 'user', content: '另一个会话' }])
    expect(chat.messages.value.length).toBe(1)
    expect(chat.streamingText.value).toBe('')
    expect(chat.error.value).toBe('')
  })

  it('hydrate：灌入的消息过 schema 归一化（顶层 chartType 写法映射为 option）', () => {
    /* 回归：hydrate 曾直接 list.slice() 绕过 normalizeMessage（push/load 都过），
       后端按标准 schema（REQUIREMENTS §8.2）发顶层 chartType/series/categories
       的历史消息，会话切换后图表渲染成「无数据」——只在 hydrate 这一条路复现。
       断言绑定业务语义：灌入的顶层写法必须被映射成组件渲染器认的 option。 */
    const chat = useChat({ persist: false, history: { enabled: false } } as never)
    chat.hydrate([
      {
        id: 'h-chart',
        role: 'assistant',
        content: '',
        blocks: [
          { type: 'chart', chartType: 'bar', categories: ['一月', '二月'], series: [12, 30] },
        ],
      },
    ])
    const block = (chat.messages.value[0].blocks as Array<Record<string, unknown>>)[0]
    expect(block.type).toBe('chart')
    // 归一化后 option.type / option.data 必须就位（BlockRenderer 只读 option）
    const option = block.option as Record<string, unknown>
    expect(option.type).toBe('bar')
    expect(option.data).toEqual([
      { name: '一月', value: 12 },
      { name: '二月', value: 30 },
    ])
  })
})
