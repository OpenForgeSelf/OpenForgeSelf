import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import ChatRecordsView from '@/views/ChatRecordsView.vue'
import type { ChatSessionDetail, ChatSessionSummary } from '@/types/chatRecords'

// vi.mock 工厂会被提升到文件顶部，必须在 hoisted 作用域内定义待引用的 mock，
// 否则顶层 const 处于 TDZ 触发「Cannot access before initialization」。
const { subscribeMock } = vi.hoisted(() => ({ subscribeMock: vi.fn() }))

function makeSession(id: number): ChatSessionSummary {
  return {
    id,
    sessionKey: `sess-key-${id}`,
    source: 'Proxy',
    title: null,
    model: 'gpt-4o',
    provider: null,
    style: 'OpenAI_Chat',
    clientKind: 'Curl',
    requestCount: 2,
    messageCount: 4,
    firstUserMsg: `首条${id}`,
    totalPromptTokens: 100,
    totalCompletionTokens: 50,
    lastStatus: 200,
    createdTime: '2026-08-07T10:00:00Z',
    updatedTime: '2026-08-07T10:05:00Z'
  }
}

function makeDetail(id: number): ChatSessionDetail {
  return {
    session: makeSession(id),
    turns: []
  }
}

vi.mock('@/services/chatRecordsApi', () => ({
  chatRecordsApi: {
    getSessions: vi.fn(),
    getSessionById: vi.fn()
  }
}))

vi.mock('@/services/websocket', () => ({
  wsService: {
    connect: vi.fn(),
    subscribe: subscribeMock,
    unsubscribe: vi.fn()
  }
}))

import { chatRecordsApi } from '@/services/chatRecordsApi'

describe('ChatRecordsView WebSocket 推送接通', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    ;(chatRecordsApi.getSessions as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      sessions: [makeSession(1), makeSession(2)],
      total: 2,
      page: 1,
      pageSize: 20,
      pageCount: 1
    })
    ;(chatRecordsApi.getSessionById as unknown as ReturnType<typeof vi.fn>).mockImplementation(
      (id: number) => Promise.resolve(makeDetail(id))
    )
  })

  async function setup() {
    const wrapper = mount(ChatRecordsView)
    await flushPromises()
    await nextTick()
    return wrapper
  }

  it('WebSocket 实时推送接通：收到 chat_record_chunk 出现实时卡片', async () => {
    const wrapper = await setup()
    const handler = subscribeMock.mock.calls[0][0] as (msg: unknown) => void
    handler({ type: 'chat_record_chunk', requestId: 'r1', sessionId: 's1', text: '正在生成…' })
    await nextTick()
    expect(wrapper.find('.live-stream-card').exists()).toBe(true)
    expect(wrapper.text()).toContain('正在生成…')
  })

  it('chat_record_completed：标记实时卡片完成并刷新会话列表（不整页崩溃）', async () => {
    const wrapper = await setup()
    const handler = subscribeMock.mock.calls[0][0] as (msg: unknown) => void
    // 先发一个 chunk 建立 liveStream
    handler({ type: 'chat_record_chunk', requestId: 'r2', sessionId: 's2', text: 'abc' })
    await nextTick()
    // 再发完成
    handler({ type: 'chat_record_completed', requestId: 'r2', sessionId: 's2', recordId: 999, isDone: true })
    await flushPromises()
    await nextTick()
    // 完成触发了会话列表刷新（初始 1 次 + 完成 1 次）
    expect(chatRecordsApi.getSessions).toHaveBeenCalledTimes(2)
    // 实时卡片标记为完成
    expect(wrapper.find('.live-badge').text()).toContain('完成')
  })

  it('completed 携带 error：实时卡片标记为失败', async () => {
    const wrapper = await setup()
    const handler = subscribeMock.mock.calls[0][0] as (msg: unknown) => void
    handler({ type: 'chat_record_chunk', requestId: 'r3', sessionId: 's3', text: 'x' })
    await nextTick()
    handler({ type: 'chat_record_completed', requestId: 'r3', sessionId: 's3', recordId: 1, isDone: true, error: true })
    await flushPromises()
    await nextTick()
    expect(wrapper.find('.live-badge').text()).toContain('失败')
  })
})
