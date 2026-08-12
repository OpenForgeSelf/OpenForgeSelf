// 聊天记录前端类型：以「会话（ChatSession）+ 轮次（ChatTurn）」两级结构对齐后端。
// 后端实体 JSON 为 PascalCase（默认 System.Text.Json），前端在此做一次映射收口。

export type ApiStyle = 'OpenAI_Chat' | 'OpenAI_Responses' | 'Anthropic_Messages' | 'AppChat' | string

export type SessionSource = 'App' | 'Proxy' | string

/** 会话摘要（列表维度）。字段与后端 ChatSession 实体对齐。 */
export interface ChatSessionSummary {
  id: number
  sessionKey: string
  source: SessionSource
  title: string | null
  model: string | null
  provider: string | null
  style: ApiStyle | null
  clientKind: string
  requestCount: number
  messageCount: number
  firstUserMsg: string | null
  totalPromptTokens: number
  totalCompletionTokens: number
  lastStatus: number
  createdTime: string
  updatedTime: string
}

/** 轮次（详情维度，原单条 ChatRecord）。字段与后端 ChatTurn 实体对齐。 */
export interface ChatTurn {
  id: number
  chatSessionId: number
  turnIndex: number
  sessionKey: string
  style: ApiStyle | null
  model: string | null
  requestMethod: string | null
  requestPath: string | null
  requestHeaders: string | null
  requestBody: string | null
  responseStatus: number
  responseHeaders: string | null
  responseBody: string | null
  requestId: string | null
  responseText: string | null
  userPreview: string | null
  assistantPreview: string | null
  promptTokens: number
  completionTokens: number
  totalTokens: number
  firstTokenMs: number | null
  errorMessage: string | null
  temperature: number
  maxTokens: number | null
  messageCount: number
  toolCallCount: number
  hasReasoning: boolean
  durationMs: number
  createdTime: string
}

/** 会话列表分页响应。 */
export interface ChatSessionsResponse {
  sessions: ChatSessionSummary[]
  total: number
  page: number
  pageSize: number
  pageCount: number
}

/** 会话详情：会话本身 + 其下轮次明细（按 TurnIndex 升序）。 */
export interface ChatSessionDetail {
  session: ChatSessionSummary
  turns: ChatTurn[]
}
