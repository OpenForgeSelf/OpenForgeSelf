export type ApiStyle = 'OpenAI_Chat' | 'OpenAI_Responses' | 'Anthropic_Messages'

export interface ChatRecord {
  id: number
  sessionId: string
  style: ApiStyle
  model: string
  requestMethod: string
  requestPath: string
  requestHeaders: Record<string, string>
  requestBody: Record<string, any>
  responseStatus: number
  responseHeaders: Record<string, string>
  responseBody: unknown
  temperature: number
  maxTokens?: number
  messageCount: number
  toolCallCount: number
  hasReasoning: boolean
  durationMs: number
  createdTime: string
}

export interface ChatRecordSummary {
  id: number
  sessionId: string
  style: ApiStyle
  model: string
  messageCount: number
  toolCallCount: number
  createdTime: string
  summary: string
}

export interface ChatRecordsResponse {
  records: ChatRecordSummary[]
  total: number
  page: number
  pageSize: number
}
