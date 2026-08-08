import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import ChatRecordsView from '@/views/ChatRecordsView.vue'
import type { ChatRecord, ChatRecordsResponse } from '@/types/chatRecords'

// vi.mock 工厂会被提升到文件顶部，必须在 hoisted 作用域内定义待引用的 mock，
// 否则顶层 const 处于 TDZ 触发「Cannot access before initialization」。
const { subscribeMock } = vi.hoisted(() => ({ subscribeMock: vi.fn() }))

function makeSummary(id: number) {
  return {
    id,
    sessionId: `sess-${id}`,
    style: 'OpenAI_Chat',
    model: 'gpt-4o',
    summary: `摘要${id}`,
    messageCount: 2,
    toolCallCount: 0,
    createdTime: '2026-08-07T10:00:00Z'
  }
}

function makeFullRecord(id: number) {
  return {
    id,
    sessionId: `sess-${id}`,
    style: 'OpenAI_Chat',
    model: 'gpt-4o',
    requestMethod: 'POST',
    requestPath: '/v1/chat/completions',
    requestHeaders: {},
    requestBody: { messages: [{ role: 'user', content: 'hi' }] },
    responseStatus: 200,
    responseHeaders: {},
    responseBody: { choices: [{ finish_reason: 'stop', message: { content: 'ok' } }] },
    temperature: 0.7,
    maxTokens: 1024,
    messageCount: 2,
    toolCallCount: 0,
    hasReasoning: false,
    durationMs: 100,
    createdTime: '2026-08-07T10:00:00Z'
  } as ChatRecord
}

vi.mock('@/services/chatRecordsApi', () => ({
  chatRecordsApi: {
    getRecords: vi.fn(),
    getRecordById: vi.fn()
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
    ;(chatRecordsApi.getRecords as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      records: [makeSummary(1), makeSummary(2)],
      total: 2
    } as ChatRecordsResponse)
    ;(chatRecordsApi.getRecordById as unknown as ReturnType<typeof vi.fn>).mockImplementation(
      (id: number) => Promise.resolve(makeFullRecord(id))
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

  it('chat_record_completed：用 recordId 精准拉取并插入列表顶部（不整页刷新）', async () => {
    const wrapper = await setup()
    const handler = subscribeMock.mock.calls[0][0] as (msg: unknown) => void
    // 先发一个 chunk 建立 liveStream
    handler({ type: 'chat_record_chunk', requestId: 'r2', sessionId: 's2', text: 'abc' })
    await nextTick()
    // 再发完成，携带 recordId=999
    handler({ type: 'chat_record_completed', requestId: 'r2', sessionId: 's2', recordId: 999, isDone: true })
    await flushPromises()
    await nextTick()
    // 精准拉取了新记录
    expect(chatRecordsApi.getRecordById).toHaveBeenCalledWith(999)
    // 新记录插到列表顶部
    const firstRow = wrapper.findAll('.record-row')[0]
    expect(firstRow.find('.col-id').text()).toBe('999')
    // 总数 +1（原 2 → 3）
    expect(wrapper.text()).toContain('共 3 条记录')
  })

  it('completed 无 recordId 时降级全量刷新', async () => {
    await setup()
    const handler = subscribeMock.mock.calls[0][0] as (msg: unknown) => void
    handler({ type: 'chat_record_completed', requestId: 'r3', sessionId: 's3', isDone: true })
    await flushPromises()
    await nextTick()
    // 没有精准 id，走全量刷新
    expect(chatRecordsApi.getRecords).toHaveBeenCalled()
  })
})
