import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import ChatRecordDetail from './ChatRecordDetail.vue'
import type { ChatTurn } from '@/types/chatRecords'

// 后端落库的 requestBody / responseBody 为 JSON 字符串，测试同样以字符串传入。
const j = (o: unknown): string => JSON.stringify(o)

function makeRecord(partial: Partial<ChatTurn> = {}): ChatTurn {
  return {
    id: 1,
    chatSessionId: 1,
    turnIndex: 1,
    sessionKey: 'sess-0001',
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
    durationMs: 1234,
    createdTime: '2026-08-07T10:00:00Z',
    ...partial
  } as ChatTurn
}

describe('ChatRecordDetail 轮次对话气泡模式', () => {
  it('头部展示 id / model / style / session / HTTP 状态', () => {
    const wrapper = mount(ChatRecordDetail, { props: { record: makeRecord({}) } })
    const text = wrapper.text()
    expect(text).toContain('聊天记录详情')
    expect(text).toContain('#1')
    expect(text).toContain('HTTP 200')
    expect(text).toContain('gpt-4o')
    expect(text).toContain('OpenAI_Chat')
    expect(text).toContain('sess-0001')
    expect(text).toContain('1.23s')
  })

  it('对话气泡：user 在右、assistant 在左，头像按角色区分（用户 / AI）', () => {
    const wrapper = mount(ChatRecordDetail, {
      props: {
        record: makeRecord({
          requestBody: j({
            messages: [
              { role: 'user', content: '北京天气？' },
              { role: 'assistant', content: '好的' }
            ]
          }),
          responseBody: j({ choices: [{ finish_reason: 'stop', message: { content: '已查询' } }] })
        })
      }
    })
    const text = wrapper.text()
    // 角色标签
    expect(text).toContain('用户')
    expect(text).toContain('AI')
    // 头像元素按角色
    expect(wrapper.find('.avatar-user').exists()).toBe(true)
    expect(wrapper.find('.avatar-ai').exists()).toBe(true)
    // 左右布局：user 行 row-user、assistant 行 row-other
    expect(wrapper.find('.row-user').exists()).toBe(true)
    expect(wrapper.find('.row-other').exists()).toBe(true)
    // 气泡内容
    expect(text).toContain('北京天气？')
    expect(text).toContain('已查询')
  })

  it('system 消息以居中灰条展示', () => {
    const wrapper = mount(ChatRecordDetail, {
      props: {
        record: makeRecord({
          requestBody: j({ messages: [{ role: 'system', content: '你是助手' }] })
        })
      }
    })
    expect(wrapper.find('.sys-bar').exists()).toBe(true)
    expect(wrapper.text()).toContain('系统提示')
    expect(wrapper.text()).toContain('你是助手')
  })

  it('工具调用在 assistant 气泡内可见，工具结果以独立气泡呈现', () => {
    const wrapper = mount(ChatRecordDetail, {
      props: {
        record: makeRecord({
          requestBody: j({
            messages: [
              { role: 'user', content: '查天气' },
              {
                role: 'assistant',
                content: '好的',
                tool_calls: [{ id: 'call_1', type: 'function', function: { name: 'getWeather', arguments: '{"city":"BJ"}' } }]
              },
              { role: 'tool', tool_call_id: 'call_1', content: '晴 26°C' }
            ]
          }),
          responseBody: j({
            choices: [{ finish_reason: 'stop', message: { content: '已查询' } }],
            usage: { prompt_tokens: 10, completion_tokens: 5, total_tokens: 15 }
          }),
          toolCallCount: 1
        })
      }
    })
    const text = wrapper.text()
    // 工具调用（嵌 assistant 气泡）
    expect(wrapper.find('.tool-call').exists()).toBe(true)
    expect(text).toContain('getWeather')
    // 工具结果独立气泡
    expect(text).toContain('✅ 工具结果')
    expect(text).toContain('晴 26°C')
    // 响应 meta：finish + usage
    expect(text).toContain('finish: stop')
    expect(text).toContain('tokens:')
    expect(text).toContain('10')
    expect(text).toContain('15')
  })

  it('原始数据 tab：点击切换到完整 JSON 视图', async () => {
    const wrapper = mount(ChatRecordDetail, {
      props: {
        record: makeRecord({
          requestBody: j({ messages: [{ role: 'user', content: 'hi' }] })
        })
      }
    })
    const tabs = wrapper.findAll('.tab-btn')
    expect(tabs).toHaveLength(2)
    // 默认对话视图 active
    expect(tabs[0].classes()).toContain('active')
    expect(tabs[1].classes()).not.toContain('active')
    // 点击原始数据
    await tabs[1].trigger('click')
    expect(tabs[1].classes()).toContain('active')
    expect(tabs[0].classes()).not.toContain('active')
    // 原始视图含完整请求体 / 响应体 区块
    expect(wrapper.find('.raw-view').exists()).toBe(true)
    expect(wrapper.text()).toContain('完整请求体')
    expect(wrapper.text()).toContain('完整响应体')
  })

  it('流式分片（后端拼接 chunk 数组）：合并 delta、提取 finish_reason、未回传 usage 不崩溃', () => {
    const wrapper = mount(ChatRecordDetail, {
      props: {
        record: makeRecord({
          responseBody: j([
            { choices: [{ delta: { role: 'assistant' } }] },
            { choices: [{ delta: { content: '你好' } }] },
            { choices: [{ delta: { content: '世界' } }] },
            { choices: [{ delta: {}, finish_reason: 'stop' }] }
          ])
        })
      }
    })
    const text = wrapper.text()
    expect(text).toContain('你好世界')
    expect(text).toContain('finish: stop')
    expect(text).toContain('未回传 usage')
  })

  it('错误对象响应：不崩溃，错误文本可见', () => {
    const wrapper = mount(ChatRecordDetail, {
      props: {
        record: makeRecord({
          responseStatus: 500,
          responseBody: j([{ error: 'Response status code does not indicate success: 500 (Internal Server Error).' }])
        })
      }
    })
    const text = wrapper.text()
    expect(text).toContain('Internal Server Error')
    expect(text).toContain('未回传 usage')
    expect(text).toContain('finish: 无')
  })

  it('空响应体：占位提示，不崩溃', () => {
    const wrapper = mount(ChatRecordDetail, {
      props: { record: makeRecord({ responseBody: j([]) }) }
    })
    const text = wrapper.text()
    expect(text).toContain('无结构化对话内容')
  })
})
