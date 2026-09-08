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

/** Agent 流式聊天请求体（对应后端 ChatRequest）。 */
export interface AgentChatPayload {
  sessionId: string
  message: string
  chatModelId?: string
  agentId?: string
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
export async function streamAgentChat(payload: AgentChatPayload, handlers: AgentStreamHandlers): Promise<void> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch('/api/ai-agent/chat/stream', {
    method: 'POST',
    headers,
    body: JSON.stringify(payload),
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

  const json = (await res.json()) as unknown
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
