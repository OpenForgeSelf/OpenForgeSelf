import type {
  ApiStyle,
  ChatSessionDetail,
  ChatSessionSummary,
  ChatSessionsResponse,
  ChatTurn
} from '@/types/chatRecords'

const API_BASE = '/api/chat-sessions'

export const chatRecordsApi = {
  async getSessions(params: {
    source?: string
    clientKind?: string
    style?: string
    from?: string
    to?: string
    key?: string
    page?: number
    pageSize?: number
  }): Promise<ChatSessionsResponse> {
    const query = new URLSearchParams()
    if (params.source) query.append('source', params.source)
    if (params.clientKind) query.append('clientKind', params.clientKind)
    if (params.style) query.append('style', params.style)
    if (params.from) query.append('from', params.from)
    if (params.to) query.append('to', params.to)
    if (params.key) query.append('key', params.key)
    if (params.page) query.append('page', String(params.page))
    if (params.pageSize) query.append('pageSize', String(params.pageSize))
    const res = await fetch(`${API_BASE}?${query}`)
    const json = await res.json()
    const sessions = (json.data || []).map((item: Record<string, unknown>): ChatSessionSummary => ({
      id: item.id as number,
      sessionKey: item.sessionKey as string,
      source: item.source as string,
      title: (item.title as string) || null,
      model: (item.model as string) || null,
      provider: (item.provider as string) || null,
      style: (item.style as ApiStyle) || null,
      clientKind: item.clientKind as string,
      requestCount: item.requestCount as number,
      messageCount: item.messageCount as number,
      firstUserMsg: (item.firstUserMsg as string) || null,
      totalPromptTokens: item.totalPromptTokens as number,
      totalCompletionTokens: item.totalCompletionTokens as number,
      lastStatus: item.lastStatus as number,
      createdTime: item.createdTime as string,
      updatedTime: item.updatedTime as string
    }))
    return {
      sessions,
      total: json.total || 0,
      page: json.page || 1,
      pageSize: json.pageSize || 20,
      pageCount: json.pageCount || 1
    }
  },

  async getSessionById(id: number): Promise<ChatSessionDetail> {
    const res = await fetch(`${API_BASE}/${id}`)
    const json = await res.json()
    const data = json.data
    const session: ChatSessionSummary = {
      id: data.session.id,
      sessionKey: data.session.sessionKey,
      source: data.session.source,
      title: data.session.title || null,
      model: data.session.model || null,
      provider: data.session.provider || null,
      style: data.session.style || null,
      clientKind: data.session.clientKind,
      requestCount: data.session.requestCount,
      messageCount: data.session.messageCount,
      firstUserMsg: data.session.firstUserMsg || null,
      totalPromptTokens: data.session.totalPromptTokens,
      totalCompletionTokens: data.session.totalCompletionTokens,
      lastStatus: data.session.lastStatus,
      createdTime: data.session.createdTime,
      updatedTime: data.session.updatedTime
    }
    const turns: ChatTurn[] = (data.turns || []).map((t: Record<string, unknown>): ChatTurn => ({
      id: t.id as number,
      chatSessionId: t.chatSessionId as number,
      turnIndex: t.turnIndex as number,
      sessionKey: t.sessionKey as string,
      style: (t.style as ApiStyle) || null,
      model: (t.model as string) || null,
      requestMethod: (t.requestMethod as string) || null,
      requestPath: (t.requestPath as string) || null,
      requestHeaders: (t.requestHeaders as string) || null,
      requestBody: (t.requestBody as string) || null,
      responseStatus: t.responseStatus as number,
      responseHeaders: (t.responseHeaders as string) || null,
      responseBody: (t.responseBody as string) || null,
      requestId: (t.requestId as string) || null,
      responseText: (t.responseText as string) || null,
      userPreview: (t.userPreview as string) || null,
      assistantPreview: (t.assistantPreview as string) || null,
      promptTokens: t.promptTokens as number,
      completionTokens: t.completionTokens as number,
      totalTokens: t.totalTokens as number,
      firstTokenMs: (t.firstTokenMs as number) ?? null,
      errorMessage: (t.errorMessage as string) || null,
      temperature: t.temperature as number,
      maxTokens: (t.maxTokens as number) ?? null,
      messageCount: t.messageCount as number,
      toolCallCount: t.toolCallCount as number,
      hasReasoning: t.hasReasoning as boolean,
      durationMs: t.durationMs as number,
      createdTime: t.createdTime as string
    }))
    return { session, turns }
  }
}
