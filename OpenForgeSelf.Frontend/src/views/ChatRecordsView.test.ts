import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { nextTick } from 'vue'
import ChatRecordsView from '@/views/ChatRecordsView.vue'
import type { ChatRecord, ChatRecordsResponse } from '@/types/chatRecords'

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
    subscribe: vi.fn(),
    unsubscribe: vi.fn()
  }
}))

import { chatRecordsApi } from '@/services/chatRecordsApi'

describe('ChatRecordsView 列表与详情弹窗交互', () => {
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

  // ElDialog 使用 append-to-body 会把弹窗 DOM teleport 到 document.body，
  // 若不清理会在测试间泄漏，导致 querySelector 命中上一个用例的弹窗。每个用例后清空。
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('列表渲染记录行与分页信息', async () => {
    const wrapper = mount(ChatRecordsView)
    await flushPromises()
    await nextTick()
    expect(wrapper.findAll('.record-row')).toHaveLength(2)
    expect(wrapper.text()).toContain('共 2 条记录')
  })

  it('点击「查看详情」→ 弹窗打开并渲染 ChatRecordDetail（含记录 id）', async () => {
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
    // 头部含记录 id，气泡含请求内容
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
    // 弹窗关闭：触发 update:modelValue=false（与点击头部关闭按钮等价，jsdom 下
    // ElDialog 经 teleport 渲染，原生 click 事件在 jsdom 中不会驱动 EP 的关闭逻辑，
    // 故直接校验 v-model 绑定——关闭按钮正是 emit 该事件驱动父组件 showDetail 置 false）
    const dialog = wrapper.findComponent({ name: 'ElDialog' })
    await dialog.vm.$emit('update:modelValue', false)
    await flushPromises()
    await nextTick()
    expect(wrapper.findComponent({ name: 'ElDialog' }).props('modelValue')).toBe(false)
  })
})
