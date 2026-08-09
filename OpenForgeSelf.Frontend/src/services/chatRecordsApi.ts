import type { ApiStyle, ChatRecord, ChatRecordSummary, ChatRecordsResponse } from '@/types/chatRecords'
import { parseJsonSequence } from '@/utils/jsonSequence'

const API_BASE = '/api/chat-records'

// 从请求体（可能是单个 JSON 对象或流式拼接）中提取一条可读摘要：
// 优先取首个 user/system 消息文本；其次 OpenAI Responses 的 input message；再次 Anthropic 的顶层 system。
function summarizeRequest(reqBodyRaw: unknown): string {
  try {
    const parsed = parseJsonSequence(typeof reqBodyRaw === 'string' ? reqBodyRaw : JSON.stringify(reqBodyRaw))
    const first = parsed[0] as Record<string, unknown> | undefined
    if (!first) return ''

    const strip = (t: unknown): string => {
      if (typeof t !== 'string') return ''
      return t.replace(/\s+/g, ' ').substring(0, 100)
    }
    const arrayText = (content: unknown): string => {
      if (!Array.isArray(content)) return ''
      return content
        .map((p) => (p && typeof p === 'object' && 'text' in p ? (p as { text?: unknown }).text : ''))
        .filter((t) => typeof t === 'string' && t)
        .join(' ')
    }

    const msgs = first['messages']
    if (Array.isArray(msgs)) {
      for (const m of msgs as Record<string, unknown>[]) {
        const role = m['role']
        if (role === 'user' || role === 'system') {
          const c = m['content']
          const txt = typeof c === 'string' ? c : arrayText(c)
          if (txt.trim()) return strip(txt)
        }
      }
    }
    const input = first['input']
    if (Array.isArray(input)) {
      for (const it of input as Record<string, unknown>[]) {
        if (it['type'] === 'message') {
          const txt = arrayText(it['content'])
          if (txt.trim()) return strip(txt)
        }
      }
    }
    if (typeof first['system'] === 'string') return strip(first['system'])
  } catch {
    // 解析失败则回退为空摘要
  }
  return ''
}

export const chatRecordsApi = {
  async getRecords(params: {
    sessionId?: string
    style?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  }): Promise<ChatRecordsResponse> {
    const query = new URLSearchParams()
    if (params.sessionId) query.append('sessionId', params.sessionId)
    if (params.style) query.append('style', params.style)
    if (params.from) query.append('from', params.from)
    if (params.to) query.append('to', params.to)
    if (params.page) query.append('page', String(params.page))
    if (params.pageSize) query.append('pageSize', String(params.pageSize))
    const res = await fetch(`${API_BASE}?${query}`)
    const json = await res.json()
    return {
      records: (json.data || []).map((item: Record<string, unknown>): ChatRecordSummary => ({
        id: item.id as number,
        sessionId: item.sessionId as string,
        style: item.style as ApiStyle,
        model: item.model as string,
        messageCount: item.messageCount as number,
        toolCallCount: item.toolCallCount as number,
        createdTime: item.createdTime as string,
        summary: summarizeRequest(item.requestBody)
      })),
      total: json.total || 0,
      page: json.page || 1,
      pageSize: json.pageSize || 20
    }
  },

  async getRecordById(id: number): Promise<ChatRecord> {
    const res = await fetch(`${API_BASE}/${id}`)
    const json = await res.json()
    const data = json.data
    return {
      id: data.id,
      sessionId: data.sessionId,
      style: data.style,
      model: data.model,
      requestMethod: data.requestMethod,
      requestPath: data.requestPath,
      requestHeaders: typeof data.requestHeaders === 'string' ? JSON.parse(data.requestHeaders || '{}') : (data.requestHeaders || {}),
      // 请求体恒为单对象：取序列首对象
      requestBody: parseJsonSequence(data.requestBody as string | null)[0] ?? {},
      responseStatus: data.responseStatus,
      responseHeaders: typeof data.responseHeaders === 'string' ? JSON.parse(data.responseHeaders || '{}') : (data.responseHeaders || {}),
      // 响应体可能是「单对象 / 流式分片拼接 / 错误对象」：保留为对象数组，由详情组件归一化
      responseBody: parseJsonSequence(data.responseBody as string | null),
      temperature: data.temperature,
      maxTokens: data.maxTokens,
      messageCount: data.messageCount,
      toolCallCount: data.toolCallCount,
      hasReasoning: data.hasReasoning,
      durationMs: data.durationMs,
      createdTime: data.createdTime
    }
  }
}
