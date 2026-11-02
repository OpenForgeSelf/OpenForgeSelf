/**
 * IM 网关插件界面用的极简 HTTP 封装。
 *
 * 与宿主/其他插件一致：直连后端 HTTP 接口，从 localStorage 读取宿主写入的 token
 * （键名与 ForgeSelf.Web/src/services/request.ts 保持一致）。
 *
 * 接口契约（后端 ImGatewayController）：
 * - GET   /api/im-gateway/config         → ImGatewayConfig（企微凭证）
 * - POST  /api/im-gateway/config         → { ok: true }（保存）
 * - GET   /api/im-gateway/status         → [{ type, name, enabled }]（通道启用状态）
 * - POST  /api/im-gateway/wecom/reconnect → { ok: true }（手动重连长连接，D2）
 * - POST  /api/im-gateway/wecom/scan-auth   → { id, state, qrLink, qrText }（发起扫码授权）
 * - GET   /api/im-gateway/wecom/scan-auth/{id} → { id, state, qrLink, qrText, botId, error }（轮询扫码状态）
 * - GET   /api/im-gateway/conversations    → [{ channelKey, channelType, displayName, sessionId, lastMessage, lastActiveAt, messageCount }]（会话列表，PILOT-078）
 * - GET   /api/im-gateway/messages?channelKey=&limit=&beforeId= → { messages: [...], hasMore }（消息线程游标分页，PILOT-078）
 */
const TOKEN_KEY = 'forge_api_token'

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
      /* 非 JSON 响应忽略 */
    }
    throw new Error(`请求失败(${res.status}): ${detail || res.statusText || '未知错误'}`)
  }
  if (res.status === 204) return undefined
  return (await res.json().catch(() => undefined)) as T | undefined
}

/** 读取全部通道配置。 */
export function fetchConfig(): Promise<unknown> {
  return request<unknown>('/api/im-gateway/config', { method: 'GET' })
}

/** 保存全部通道配置。 */
export function saveConfig(config: unknown): Promise<{ ok?: boolean } | undefined> {
  return request<{ ok?: boolean }>('/api/im-gateway/config', { method: 'POST', body: JSON.stringify(config) })
}

/** 读取通道启用状态。 */
export function fetchStatus(): Promise<unknown> {
  return request<unknown>('/api/im-gateway/status', { method: 'GET' })
}

/** 手动重连企微长连接（D2：被踢后重新抢占 / 立即重连不等退避）。 */
export function reconnectWeCom(): Promise<{ ok?: boolean } | undefined> {
  return request<{ ok?: boolean }>('/api/im-gateway/wecom/reconnect', { method: 'POST' })
}

/** 扫码授权会话（后端驱动全流程：CLI 扫码 → 解密 → 自动回填保存）。 */
export interface ScanAuthDto {
  id: string
  state: 'starting' | 'waitingscan' | 'succeeded' | 'failed'
  qrLink?: string | null
  qrText?: string | null
  botId?: string | null
  error?: string | null
}

/** 发起扫码授权。 */
export function startScanAuth(): Promise<ScanAuthDto | undefined> {
  return request<ScanAuthDto>('/api/im-gateway/wecom/scan-auth', { method: 'POST' })
}

/** 轮询扫码授权状态。 */
export function fetchScanAuth(id: string): Promise<ScanAuthDto | undefined> {
  return request<ScanAuthDto>(`/api/im-gateway/wecom/scan-auth/${id}`, { method: 'GET' })
}

/** 会话列表条目的「最后一条消息」摘要（PILOT-078 FR-2；零消息时为 null）。 */
export interface ConversationLastMessageDto {
  direction: 'in' | 'out'
  status: 'ok' | 'error'
  /** 后端已截 60 字符的预览文本。 */
  preview: string
  createdAt: string
}

/** 会话列表条目（ImGatewaySession 每行 = 1 个联系人）。 */
export interface ConversationDto {
  channelKey: string
  channelType: string
  /** ChannelKey 末段（PlatformUserId）。 */
  displayName: string
  sessionId?: string | null
  lastMessage?: ConversationLastMessageDto | null
  lastActiveAt: string
  messageCount: number
}

/** 消息线程条目（PILOT-078 FR-3）。 */
export interface ChatMessageDto {
  id: number
  channelKey: string
  channelType: string
  /** in=收到（左气泡）/ out=回复（右气泡）。 */
  direction: 'in' | 'out'
  content: string
  sessionId?: string | null
  platformMsgId?: string | null
  /** ok / error（error 气泡带失败标记 + errorText 摘要）。 */
  status: 'ok' | 'error'
  errorText?: string | null
  createdAt: string
}

/** 消息线程分页结果（升序输出）。 */
export interface MessagesPageDto {
  messages: ChatMessageDto[]
  hasMore: boolean
}

/** 读取会话列表（已绑定 IM 联系人，按最近消息倒序，无消息排后）。 */
export function fetchConversations(): Promise<ConversationDto[] | undefined> {
  return request<ConversationDto[]>('/api/im-gateway/conversations', { method: 'GET' })
}

/**
 * 读取消息线程：beforeId 之前（不含）的最新 limit 条，按 Id 升序输出 + hasMore。
 * channelKey 走 query 参数（避开路由编码）。
 */
export function fetchMessages(channelKey: string, limit = 50, beforeId?: number): Promise<MessagesPageDto | undefined> {
  const qs = new URLSearchParams({ channelKey, limit: String(limit) })
  if (beforeId && beforeId > 0) qs.set('beforeId', String(beforeId))
  return request<MessagesPageDto>(`/api/im-gateway/messages?${qs.toString()}`, { method: 'GET' })
}

/** 读取本插件当前运行版本（宿主 /api/plugin 清单），用于界面展示「自身版本」。 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const resp = await request<{ data?: Array<{ id: string; version: string }> } | Array<{ id: string; version: string }>>('/api/plugin', { method: 'GET' })
  const list = Array.isArray(resp) ? resp : (resp?.data ?? [])
  const mine = list.find((p) => p?.id === pluginId)
  return mine?.version ?? ''
}
