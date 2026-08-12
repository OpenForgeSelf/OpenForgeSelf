import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import ChatRecordsView from '@/views/ChatRecordsView.vue'
import type { ChatSessionDetail, ChatSessionSummary, ChatTurn } from '@/types/chatRecords'

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

function makeTurn(id: number, partial: Partial<ChatTurn> = {}): ChatTurn {
  return {
    id,
    chatSessionId: 1,
    turnIndex: 1,
    sessionKey: 'sess-key-1',
    style: 'OpenAI_Chat',
    model: 'gpt-4o',
    requestMethod: 'POST',
    requestPath: '/v1/chat/completions',
    requestHeaders: null,
    requestBody: null,
    responseStatus: 200,
    responseHeaders: null,
    responseBody: null,
    requestId: null,
    responseText: null,
    userPreview: null,
    assistantPreview: null,
    promptTokens: 10,
    completionTokens: 5,
    totalTokens: 15,
    firstTokenMs: null,
    errorMessage: null,
    temperature: 0.7,
    maxTokens: 1024,
    messageCount: 2,
    toolCallCount: 0,
    hasReasoning: false,
    durationMs: 100,
    createdTime: '2026-08-07T10:00:00Z',
    ...partial
  }
}

function makeDetail(id: number): ChatSessionDetail {
  return {
    session: makeSession(id),
    turns: [makeTurn(1, { requestBody: JSON.stringify({ messages: [{ role: 'user', content: 'hi' }] }) })]
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
    subscribe: vi.fn(),
    unsubscribe: vi.fn()
  }
}))

import { chatRecordsApi } from '@/services/chatRecordsApi'

describe('ChatRecordsView 会话列表与详情弹窗交互', () => {
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

  // ElDialog 使用 append-to-body 会把弹窗 DOM teleport 到 document.body，
  // 若不清理会在测试间泄漏，导致 querySelector 命中上一个用例的弹窗。每个用例后清空。
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('列表渲染会话行与分页信息', async () => {
    const wrapper = mount(ChatRecordsView)
    await flushPromises()
    await nextTick()
    expect(wrapper.findAll('.record-row')).toHaveLength(2)
    expect(wrapper.text()).toContain('共 2 条记录')
  })

  it('点击「查看详情」→ 弹窗打开并渲染首条轮次明细（含轮次 id 与请求内容）', async () => {
    const wrapper = mount(ChatRecordsView)
    await flushPromises()
    await nextTick()
    // 点击第一行详情按钮
    await wrapper.findAll('.detail-btn')[0].trigger('click')
    await flushPromises()
    await nextTick()
    // 弹窗打开（v-model 绑定）
    const dialog = wrapper.findComponent({ name: 'ElDialog' })
    expect(dialog.exists()).toBe(true)
    expect(dialog.props('modelValue')).toBe(true)
    // ElDialog append-to-body：内容经 teleport 渲染到 document.body
    const detailEl = document.querySelector('.chat-record-detail')
    expect(detailEl).not.toBeNull()
    // 首条轮次含轮次 id，气泡含请求内容
    expect(detailEl!.textContent).toContain('#1')
    expect(detailEl!.textContent).toContain('hi')
  })

  it('弹窗关闭：点击关闭按钮后 modelValue 置 false', async () => {
    const wrapper = mount(ChatRecordsView)
    await flushPromises()
    await nextTick()
    await wrapper.findAll('.detail-btn')[0].trigger('click')
    await flushPromises()
    await nextTick()
    expect(wrapper.findComponent({ name: 'ElDialog' }).props('modelValue')).toBe(true)
    // 弹窗关闭：触发 update:modelValue=false
    const dialog = wrapper.findComponent({ name: 'ElDialog' })
    await dialog.vm.$emit('update:modelValue', false)
    await flushPromises()
    await nextTick()
    expect(wrapper.findComponent({ name: 'ElDialog' }).props('modelValue')).toBe(false)
  })
})
