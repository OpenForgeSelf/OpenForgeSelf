import type { ApiStyle, ChatRecord, ChatRecordSummary, ChatRecordsResponse } from '@/types/chatRecords'

const API_BASE = '/api/chat-records'

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
        summary: (item.requestBody as { messages?: { content?: unknown }[] })?.messages?.[0]?.content?.toString().substring(0, 100) || ''
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
      requestBody: typeof data.requestBody === 'string' ? JSON.parse(data.requestBody || '{}') : (data.requestBody || {}),
      responseStatus: data.responseStatus,
      responseHeaders: typeof data.responseHeaders === 'string' ? JSON.parse(data.responseHeaders || '{}') : (data.responseHeaders || {}),
      responseBody: typeof data.responseBody === 'string' ? JSON.parse(data.responseBody || '{}') : (data.responseBody || {}),
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
