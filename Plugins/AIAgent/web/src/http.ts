/**
 * 插件界面用的极简 HTTP 封装。
 *
 * 为什么不复用宿主的 `@/services/request`：
 * `@/` 是宿主的路径别名，插件是独立构建的产物，无从解析该别名；
 * 即便强行打包进产物，也会与宿主形成两份实现（状态隔离、行为易漂移）。
 * 因此插件一律**直连后端 HTTP 接口**，与宿主共享的是后端数据而非前端代码。
 *
 * 认证：从 localStorage 读取宿主写入的 token（键名与宿主 services/request.ts 一致），
 * 有则带 Bearer 头。宿主未启用鉴权的接口即使无 token 也能正常访问。
 */

/** 宿主写入 token 的 localStorage 键名（与 ForgeSelf.Web/src/services/request.ts 保持一致）。 */
const TOKEN_KEY = 'forge_api_token'

import type {
  AgentDefinition,
  AgentRunDetailResponse,
  AgentRunListResponse,
  InterveneRequest,
  PlanCreatedPayload,
  RunRequest,
  RunStuckPayload,
  SessionSummary,
  StepCompletedPayload,
  StepStartedPayload,
  WorkflowItem,
} from './types'

/** 接口返回的标准包裹结构。 */
export interface ApiEnvelope<T> {
  code?: number
  message?: string
  data?: T
}

/**
 * 发起带 token 的 JSON 请求并返回 data 部分。
 *
 * @param path 以 / 开头的接口路径（如 /api/ai-models）
 * @param init 可选的 fetch 参数
 * @returns 响应体的 data 字段
 */
export async function apiGet<T>(path: string, init?: RequestInit): Promise<T | undefined> {
  return request<T>(path, { ...init, method: 'GET' })
}

/**
 * 发起带 token 的 JSON POST 请求并返回 data 部分。
 * 用于发送消息（POST /api/chat）等写操作。
 *
 * @param path 以 / 开头的接口路径
 * @param body 请求体，会被序列化为 JSON
 * @returns 响应体的 data 字段
 */
export async function apiPost<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: JSON.stringify(body) })
}

/**
 * 发起带 token 的 DELETE 请求并返回 data 部分。
 * 响应无 body 时返回 undefined。
 *
 * @param path 以 / 开头的接口路径
 * @returns 响应体的 data 字段（若存在）
 */
export async function apiDelete<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'DELETE' })
}

/**
 * 发起带 token 的 JSON PUT 请求并返回 data 部分。
 * 用于更新资源（如编辑 Agent 提示词）。
 *
 * @param path 以 / 开头的接口路径
 * @param body 请求体，会被序列化为 JSON
 * @returns 响应体的 data 字段
 */
export async function apiPut<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
}

/**
 * 发起带 token 的 JSON PATCH 请求并返回 data 部分。
 * 用于部分更新资源（如人工介入步骤）。
 *
 * @param path 以 / 开头的接口路径
 * @param body 请求体，会被序列化为 JSON
 * @returns 响应体的 data 字段（若存在）
 */
export async function apiPatch<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PATCH', body: JSON.stringify(body) })
}

/** Agent 流式聊天请求体（对应后端 ChatRequest）。 */
export interface AgentChatPayload {
  sessionId: string
  message: string
  chatModelId?: string
  agentId?: string
  /** 本会话启用工具名白名单（composer 🔧 多选；空/未传 = 后端默认全挂，向后兼容）。 */
  enabledToolNames?: string[]
  /** 本会话启用技能 id 列表（composer ⚡ 多选；名称+描述+路径注入 system prompt）。 */
  skillIds?: string[]
}

/** Agent 流式聊天的事件回调（对应后端结构化 SSE 事件）。 */
export interface AgentStreamHandlers {
  /** 增量 token。 */
  onContent?: (content: string) => void
  /** 一次工具调用开始。 */
  onToolCall?: (e: { name?: string; arguments?: string }) => void
  /** 一次工具调用结果。 */
  onToolResult?: (e: { name?: string; result?: string; success?: boolean }) => void
  /** token 用量。 */
  onUsage?: (usage: AgentUsage | undefined) => void
  /** 完成。 */
  onDone?: (e: { sessionId?: string; responseId?: number; usage?: AgentUsage }) => void
  /** 错误。 */
  onError?: (message: string) => void
}

/** token 用量（对应后端 UnifiedUsage，camelCase 序列化）。 */
export interface AgentUsage {
  promptTokens?: number
  completionTokens?: number
  totalTokens?: number
}

/**
 * 调插件自带的 Agent 流式聊天接口（POST /api/ai-agent/chat/stream），
 * 按 SSE 逐事件回调（content / tool_call / tool_result / usage / done / error）。
 *
 * 为什么不复用 request()：流式接口返回 text/event-stream，不是单个 JSON，
 * 需要 ReadableStream 边读边解析，不能用「读完整 body 再 JSON.parse」的封装。
 */
export async function streamAgentChat(
  payload: AgentChatPayload,
  handlers: AgentStreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch('/api/ai-agent/chat/stream', {
    method: 'POST',
    headers,
    body: JSON.stringify(payload),
    signal,
  })
  if (!res.ok) {
    let detail = ''
    try {
      const body = (await res.json()) as { message?: string; title?: string }
      detail = body?.message ?? body?.title ?? ''
    } catch {
      // 非 JSON 响应，仅用状态码兜底
    }
    throw new Error(`请求失败(${res.status}): ${detail || res.statusText || '未知错误'}`)
  }
  if (!res.body) throw new Error('响应无内容流')

  const dispatch = (dataStr: string) => {
    let obj: Record<string, unknown>
    try {
      obj = JSON.parse(dataStr) as Record<string, unknown>
    } catch {
      return
    }
    const type = obj.type as string | undefined
    switch (type) {
      case 'content':
        handlers.onContent?.((obj.content as string) ?? '')
        break
      case 'tool_call':
        handlers.onToolCall?.({ name: obj.name as string, arguments: obj.arguments as string })
        break
      case 'tool_result':
        handlers.onToolResult?.({ name: obj.name as string, result: obj.result as string, success: obj.success as boolean })
        break
      case 'usage':
        handlers.onUsage?.(obj.usage as AgentUsage | undefined)
        break
      case 'done':
        handlers.onDone?.({ sessionId: obj.sessionId as string, responseId: obj.responseId as number, usage: obj.usage as AgentUsage })
        break
      case 'error':
        handlers.onError?.((obj.content as string) ?? '未知错误')
        break
    }
  }

  const reader = res.body.getReader()
  const decoder = new TextDecoder('utf-8')
  let buffer = ''

  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    // SSE 事件以空行（\n\n）分隔；逐行取 data: 前缀。
    let idx: number
    while ((idx = buffer.indexOf('\n\n')) >= 0) {
      const rawEvent = buffer.slice(0, idx)
      buffer = buffer.slice(idx + 2)
      for (const line of rawEvent.split('\n')) {
        if (line.startsWith('data: ')) dispatch(line.slice(6))
      }
    }
  }
  // 尾部残余（最后一个无空行结尾的事件）。
  const tail = buffer.trim()
  if (tail.startsWith('data: ')) dispatch(tail.slice(6))
}

/**
 * 构建带查询参数的 URL。路径中需编码的片段与所有查询值统一 encodeURIComponent。
 *
 * @param path 以 / 开头的接口路径
 * @param query 查询参数对象（value 为 undefined/null 时忽略）
 */
export function withQuery(path: string, query: Record<string, string | number | null | undefined>): string {
  const qs = Object.entries(query)
    .filter(([, v]) => v != null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
    .join('&')
  return qs ? `${path}?${qs}` : path
}

/**
 * 统一的请求实现：附加认证头、校验状态码、自适应解包响应结构。
 *
 * 响应解包策略（重要）：
 * 后端部分接口使用标准信封 `{ code, message, data }`（如 /api/plugin 等），
 * 但部分控制器直接返回领域对象（如 ChatController 的 /api/chat、/api/chat/history）。
 * 为避免每个调用点都判断，这里做启发式解包：
 *   - 若响应体是**对象**且同时具备 `code` 与 (`data` 或 `message`) → 解包 `data`
 *   - 否则原样返回（裸对象 / 数组）
 * 这样插件对两种契约都能工作，且向后兼容已存在的信封接口。
 *
 * @param path 接口路径
 * @param init fetch 参数
 * @returns 解包后的领域对象（裸对象 / 信封的 data / undefined）
 */
async function request<T>(path: string, init: RequestInit): Promise<T | undefined> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string>) ?? {}),
  }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(path, { ...init, headers })
  if (!res.ok) {
    let detail = ''
    try {
      const body = (await res.json()) as { message?: string; title?: string }
      detail = body?.message ?? body?.title ?? ''
    } catch {
      // 响应体非 JSON 时忽略，只用状态码兜底
    }
    throw new Error(`请求失败(${res.status}): ${detail || res.statusText || '未知错误'}`)
  }
  // 204 / 空响应体：无可解包 JSON。DELETE 等端点常返回 204 No Content，
  // 直接 return undefined 避免 res.json() 抛 "Unexpected end of JSON input"。
  if (res.status === 204) return undefined
  const json = (await res.json().catch(() => undefined)) as unknown
  if (json === undefined) return undefined
  // 信封判定：是对象、含 `data`、并具备 `success`/`code` 任一标记字段。
  // 本项目后端两种信封：
  //   a) { success, message, data }（如 /api/ai-models，由 ApiResponse.Ok 序列化）
  //   b) { code, message, data }（部分较老控制器，如 ChatRecordsController）
  // 两者共同点 = 含 `data` 且含标记字段（success 或 code）。
  if (
    json !== null &&
    typeof json === 'object' &&
    !Array.isArray(json)
  ) {
    const o = json as Record<string, unknown>
    if (
      'data' in o &&
      ('success' in o || 'code' in o)
    ) {
      return (json as ApiEnvelope<T>).data
    }
  }
  // 裸对象（ChatController 等直接返回领域对象的接口）
  return json as T
}

/* ------------------------------------------------------------------ */
/* Agent CRUD（Stage 4：可编辑提示词）                                  */
/* ------------------------------------------------------------------ */

/** 获取全部 Agent（GET /api/agents）。 */
export function fetchAgents(): Promise<AgentDefinition[] | undefined> {
  return apiGet<AgentDefinition[]>('/api/agents')
}

/** 新建 Agent（POST /api/agents）。 */
export function createAgent(agent: AgentDefinition): Promise<AgentDefinition | undefined> {
  return apiPost<AgentDefinition>('/api/agents', agent)
}

/** 更新 Agent（PUT /api/agents/{id}），重点用于编辑 SystemPrompt。 */
export function updateAgent(agentId: string, agent: AgentDefinition): Promise<AgentDefinition | undefined> {
  return apiPut<AgentDefinition>(`/api/agents/${encodeURIComponent(agentId)}`, agent)
}

/** 删除 Agent（DELETE /api/agents/{id}）。 */
export function deleteAgent(agentId: string): Promise<unknown> {
  return apiDelete(`/api/agents/${encodeURIComponent(agentId)}`)
}

/* ------------------------------------------------------------------ */
/* 会话管理（T2）：历史会话列表 + 切换 + 归档                            */
/* ------------------------------------------------------------------ */

/**
 * 会话归档筛选（与后端 SessionArchivedFilter 的查询参数取值一一对应）。
 * - active：仅未归档（默认，agent 页用）
 * - archived：仅已归档
 * - all：全部（会话管理页用）
 */
export type SessionArchivedFilter = 'active' | 'archived' | 'all'

/**
 * 拉取会话列表（GET /api/ai-agent/chat/sessions，后端按 SessionId 聚合）。
 *
 * 默认只取未归档：agent 页天然不展示已归档会话（归档是软标记，不删消息）。
 *
 * @param archived 归档筛选，默认 active
 */
export function fetchSessions(
  archived: SessionArchivedFilter = 'active',
): Promise<SessionSummary[] | undefined> {
  return apiGet<SessionSummary[]>(withQuery('/api/ai-agent/chat/sessions', { archived }))
}

/**
 * 归档 / 取消归档会话（PUT /api/ai-agent/chat/session/{id}/archive）。
 * 软标记：只改会话级归档状态，不删任何消息（区别于 deleteSession 的硬删）。
 *
 * @param sessionId 会话全 id（原样透传，做前缀剥离会命中错会话）
 * @param archived true=归档，false=取消归档
 */
export function archiveSession(sessionId: string, archived = true): Promise<unknown> {
  return apiPut(`/api/ai-agent/chat/session/${encodeURIComponent(sessionId)}/archive`, { archived })
}

/** 删除会话（DELETE /api/ai-agent/chat/session/{id}；会真实删除该会话全部消息，供会话管理页使用）。 */
export function deleteSession(sessionId: string): Promise<unknown> {
  return apiDelete(`/api/ai-agent/chat/session/${encodeURIComponent(sessionId)}`)
}

/* ------------------------------------------------------------------ */
/* 工作流（跨插件读 WorkflowEngine，供 Agent 关联选择）                */
/* ------------------------------------------------------------------ */

/** WorkflowController 返回的分页信封（与后端 PagedResult<WorkflowDefinitionDto> 对应）。 */
interface WorkflowPage {
  items?: WorkflowItem[]
  total?: number
  page?: number
  pageSize?: number
}

/**
 * 拉取全部工作流一次（pageSize 取大，避免分页循环）。用于 AgentEditDialog 关联工作流多选。
 * 通过 GET /api/workflows（WorkflowEngine 的 WorkflowController），与宿主同源带 token。
 */
export async function fetchWorkflows(): Promise<WorkflowItem[]> {
  const page = await apiGet<WorkflowPage>('/api/workflows?page=1&pageSize=200')
  return page?.items ?? []
}

/* ------------------------------------------------------------------ */
/* 计划驱动执行（029）：runs API + SSE 流式                              */
/* ------------------------------------------------------------------ */

/** 计划驱动执行 SSE 事件回调（对应 AgentRunsController 结构化 SSE 事件）。 */
export interface RunStreamHandlers {
  /** 执行计划已生成（plan_created）。 */
  onPlanCreated?: (payload: PlanCreatedPayload) => void
  /** 某步骤开始执行（step_started）。 */
  onStepStarted?: (payload: StepStartedPayload) => void
  /** 某步骤完成（step_completed）。 */
  onStepCompleted?: (payload: StepCompletedPayload) => void
  /** Run 卡住等待人工介入（run_stuck）。 */
  onRunStuck?: (payload: RunStuckPayload) => void
  /** 步骤循环内增量 token（content）。 */
  onContent?: (content: string) => void
  /** 步骤循环内一次工具调用开始（tool_call）。 */
  onToolCall?: (e: { name?: string; arguments?: string }) => void
  /** 步骤循环内一次工具调用结果（tool_result）。 */
  onToolResult?: (e: { name?: string; result?: string; success?: boolean }) => void
  /** token 用量（usage）。 */
  onUsage?: (usage: AgentUsage | undefined) => void
  /** 全部步骤完成，终局合成交付（done.payload = 最终内容）。 */
  onDone?: (content: string) => void
  /** 错误（error）。 */
  onError?: (message: string) => void
}

/**
 * 调计划驱动执行 SSE 接口并逐事件回调。
 * 同时服务 POST /api/ai-agent/runs（创建）与 POST /api/ai-agent/runs/{id}/resume（恢复），
 * 两者事件结构一致（plan_created/step_started/step_completed/run_stuck/done 的 payload 为 JSON 字符串；
 * content/tool_call/tool_result/usage/error 直接字段）。
 */
async function streamRunRequest(
  path: string,
  payload: RunRequest | undefined,
  handlers: RunStreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(path, {
    method: 'POST',
    headers,
    body: payload ? JSON.stringify(payload) : undefined,
    signal,
  })
  if (!res.ok) {
    let detail = ''
    try {
      const body = (await res.json()) as { message?: string; title?: string }
      detail = body?.message ?? body?.title ?? ''
    } catch {
      // 非 JSON 响应，仅用状态码兜底
    }
    throw new Error(`请求失败(${res.status}): ${detail || res.statusText || '未知错误'}`)
  }
  if (!res.body) throw new Error('响应无内容流')

  const parsePayload = (raw: string): Record<string, unknown> | null => {
    try {
      const obj = JSON.parse(raw) as Record<string, unknown>
      return obj
    } catch {
      return null
    }
  }

  /** payload 字段为 JSON 字符串时解出对象，否则原样返回（done 的 payload 是最终内容字符串）。 */
  const dispatch = (dataStr: string) => {
    const obj = parsePayload(dataStr)
    if (!obj) return
    const type = obj.type as string | undefined
    const payloadRaw = typeof obj.payload === 'string' ? (parsePayload(obj.payload) ?? obj.payload) : obj.payload
    switch (type) {
      case 'plan_created':
        handlers.onPlanCreated?.(payloadRaw as PlanCreatedPayload)
        break
      case 'step_started':
        handlers.onStepStarted?.(payloadRaw as StepStartedPayload)
        break
      case 'step_completed':
        handlers.onStepCompleted?.(payloadRaw as StepCompletedPayload)
        break
      case 'run_stuck':
        handlers.onRunStuck?.(payloadRaw as RunStuckPayload)
        break
      case 'content':
        handlers.onContent?.((obj.content as string) ?? '')
        break
      case 'tool_call':
        handlers.onToolCall?.({ name: obj.name as string, arguments: obj.arguments as string })
        break
      case 'tool_result':
        handlers.onToolResult?.({ name: obj.name as string, result: obj.result as string, success: obj.success as boolean })
        break
      case 'usage':
        handlers.onUsage?.(obj.usage as AgentUsage | undefined)
        break
      case 'done':
        handlers.onDone?.((payloadRaw as string) ?? '')
        break
      case 'error':
        handlers.onError?.((obj.content as string) ?? '未知错误')
        break
    }
  }

  const reader = res.body.getReader()
  const decoder = new TextDecoder('utf-8')
  let buffer = ''

  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    let idx: number
    while ((idx = buffer.indexOf('\n\n')) >= 0) {
      const rawEvent = buffer.slice(0, idx)
      buffer = buffer.slice(idx + 2)
      for (const line of rawEvent.split('\n')) {
        if (line.startsWith('data: ')) dispatch(line.slice(6))
      }
    }
  }
  const tail = buffer.trim()
  if (tail.startsWith('data: ')) dispatch(tail.slice(6))
}

/** 创建计划驱动执行（POST /api/ai-agent/runs，SSE 流）。 */
export function streamAgentRun(
  payload: RunRequest,
  handlers: RunStreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  return streamRunRequest('/api/ai-agent/runs', payload, handlers, signal)
}

/** 恢复计划驱动执行（POST /api/ai-agent/runs/{id}/resume，SSE 流；仅 Stuck/Failed 允许）。 */
export function resumeAgentRun(
  id: number,
  handlers: RunStreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  return streamRunRequest(`/api/ai-agent/runs/${id}/resume`, undefined, handlers, signal)
}

/** Run 列表（GET /api/ai-agent/runs，分页 + 可选 sessionId/status 过滤）。 */
export function listRuns(
  sessionId?: string,
  status?: string,
  page = 1,
  pageSize = 20,
): Promise<AgentRunListResponse | undefined> {
  return apiGet<AgentRunListResponse>(
    withQuery('/api/ai-agent/runs', { sessionId, status, page, pageSize }),
  )
}

/** Run 详情（GET /api/ai-agent/runs/{id}，含步骤列表）。 */
export function getRunDetail(id: number): Promise<AgentRunDetailResponse | undefined> {
  return apiGet<AgentRunDetailResponse>(`/api/ai-agent/runs/${id}`)
}

/** 会话历史消息条目（宿主 ChatMessage 只读投影；B9-3 规划过程面板用）。 */
export interface ChatHistoryItem {
  id: number
  sessionId: string
  role: string
  content: string
  createTime?: string
}

/**
 * 会话历史消息只读拉取（GET /api/chat/history/{sessionId}，B9-3）：
 * 规划过程面板据此读 plan:{runId} scratch 会话的模型可见投影（user/assistant/tool）。
 */
export function getChatHistory(sessionId: string, limit = 200): Promise<ChatHistoryItem[] | undefined> {
  return apiGet<ChatHistoryItem[]>(`/api/chat/history/${encodeURIComponent(sessionId)}?limit=${limit}`)
}

/** 以同 Plan 新建 Run 从头执行（POST /api/ai-agent/runs/{id}/restart）。 */
export function restartRun(id: number): Promise<{ success?: boolean; newRunId?: number } | undefined> {
  return apiPost<{ success?: boolean; newRunId?: number }>(`/api/ai-agent/runs/${id}/restart`, {})
}

/** 取消 Run（POST /api/ai-agent/runs/{id}/cancel）。 */
export function cancelRun(id: number): Promise<{ success?: boolean } | undefined> {
  return apiPost<{ success?: boolean }>(`/api/ai-agent/runs/${id}/cancel`, {})
}

/** 人工介入步骤（PATCH /api/ai-agent/runs/{id}/steps/{index}；skip 跳过 / override 补位）。 */
export function interveneRun(
  id: number,
  stepIndex: number,
  request: InterveneRequest,
): Promise<{ success?: boolean; stepIndex?: number; status?: string } | undefined> {
  return apiPatch<{ success?: boolean; stepIndex?: number; status?: string }>(
    `/api/ai-agent/runs/${id}/steps/${stepIndex}`,
    request,
  )
}
