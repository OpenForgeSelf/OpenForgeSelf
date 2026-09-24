/**
 * Agent 中枢插件界面用的 HTTP 封装。
 *
 * 与宿主/其他插件一致：直连后端 HTTP 接口，从 localStorage 读取宿主写入的 token
 * （键名与 ForgeSelf.Web/src/services/request.ts 保持一致）。
 *
 * 后端统一响应信封 ApiResponse<T>：{ code, message, success, data }。
 * 本模块的 request() 负责剥信封——调用方只拿到 data，失败时抛 Error(message)。
 *
 * 接口契约（AgentHubAgentsController / AgentHubTasksController，前缀 api/agent-hub）：
 * - agents：GET /agents、POST /agents、GET|PUT|DELETE /agents/{id}、
 *           POST /agents/{id}/trust、GET /agents/discover、POST /agents/{id}/probe
 * - tasks ：GET /tasks、GET /tasks/stats、GET /tasks/{id}、POST /tasks、POST /tasks/{id}/cancel、
 *           GET /tasks/{id}/events、GET /tasks/{id}/stream(SSE)、
 *           GET /tasks/{id}/permissions、POST /tasks/{id}/permissions/{requestId}/resolve
 */
const TOKEN_KEY = 'forge_api_token'

/** 后端统一响应信封。 */
interface ApiEnvelope<T> {
  code?: number
  message?: string
  success?: boolean
  data?: T
}

/** 组装带鉴权的请求头。 */
function buildHeaders(extra?: HeadersInit): Record<string, string> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((extra as Record<string, string>) ?? {}),
  }
  if (token) headers.Authorization = `Bearer ${token}`
  return headers
}

/**
 * 发起请求并剥掉 ApiResponse 信封。
 *
 * @param path 相对路径（含前导 /）
 * @param init fetch 配置
 * @returns data 部分；204 或 data 缺省时返回 undefined
 * @throws Error —— 网络异常 / 非 2xx / 业务 success=false，均带可读中文信息
 */
async function request<T>(path: string, init: RequestInit = {}): Promise<T | undefined> {
  const res = await fetch(path, { ...init, headers: buildHeaders(init.headers) })

  // 先尝试解析信封（错误响应体也带 message，是本插件错误提示的主要来源）
  let envelope: ApiEnvelope<T> | undefined
  try {
    envelope = (await res.json()) as ApiEnvelope<T>
  } catch {
    // 非 JSON 响应（如 502 网关页）忽略，下面按状态码报错
  }

  if (!res.ok) {
    const detail = envelope?.message ?? res.statusText ?? '未知错误'
    throw new Error(`${detail}（HTTP ${res.status}）`)
  }
  if (res.status === 204) return undefined

  // 业务失败：HTTP 200 但 success=false（后端部分分支如此）
  if (envelope && envelope.success === false) {
    throw new Error(envelope.message ?? '操作失败')
  }
  return envelope?.data
}

// ────────────────────────────── 类型定义 ──────────────────────────────

/** 能力矩阵：六个能力面各自是否支持（缺格视为不支持）。 */
export interface CapabilityMatrixDto {
  facets?: Record<string, boolean>
  notes?: Record<string, string>
}

/** Agent 策略。 */
export interface AgentPolicyDto {
  permissionMode?: string
  timeoutSeconds?: number
  approvalTimeoutSeconds?: number
  maxConcurrency?: number
  allowedCwds?: string[]
  writableCwds?: string[]
  retryOnTransient?: boolean
}

/** 交互口（一个 agent 可以有多个交互口：CLI / ACP / HTTP）。 */
export interface AccessPointDto {
  id?: number
  agentId?: number
  vendor?: string
  mode?: string
  transport?: string
  executable?: string
  argsTemplate?: string
  envVars?: Record<string, string>
  promptInjection?: string
  outputFormat?: string
  sessionFlagTemplate?: string
  cancelSupported?: boolean
  probeArgs?: string
  health?: string
  lastProbeTime?: string
  lastVersion?: string
  lastError?: string
  isDefault?: boolean
}

/** Agent 展示模型。 */
export interface AgentDto {
  id?: number
  name?: string
  vendor?: string
  displayName?: string
  kind?: string
  tags?: string[]
  capabilities?: CapabilityMatrixDto
  defaultCwd?: string
  policy?: AgentPolicyDto
  enabled?: boolean
  priority?: number
  trusted?: boolean
  trustedScopes?: string[]
  notes?: string
  accessPoints?: AccessPointDto[]
  health?: string
  createTime?: string
  updateTime?: string
}

/** 新增 / 更新 Agent 的请求体。 */
export interface AgentSaveRequest {
  name?: string
  vendor?: string
  kind?: string
  tags?: string[]
  capabilities?: CapabilityMatrixDto
  defaultCwd?: string
  policy?: AgentPolicyDto
  enabled?: boolean
  priority?: number
  notes?: string
  /** 不传 = 按 profile 预填默认交互口；传空数组 = 显式清空 */
  accessPoints?: AccessPointDto[]
}

/** 发现候选（扫到但未登记）。 */
export interface DiscoveredAgentDto {
  vendor?: string
  displayName?: string
  executable?: string
  version?: string
  alreadyRegistered?: boolean
  profileId?: string
}

/** 探测结果。 */
export interface ProbeResultDto {
  found?: boolean
  health?: string
  version?: string
  path?: string
  error?: string
  profileWarning?: string
  elapsedMs?: number
}

/** 委派任务。 */
export interface TaskDto {
  id?: number
  taskKey?: string
  agentId?: number
  agentName?: string
  accessPointId?: number
  prompt?: string
  cwd?: string
  status?: string
  sessionRef?: string
  permissionMode?: string
  exitCode?: number
  errorCode?: string
  resultText?: string
  artifactsJson?: string
  usageJson?: string
  createdBy?: string
  startTime?: string
  endTime?: string
  elapsedMs?: number
  lastSeq?: number
  createTime?: string
  message?: string
}

/** 发起委派的请求体。 */
export interface DelegationRequest {
  prompt: string
  agentId?: number
  accessPointId?: number
  cwd?: string
  permissionMode?: string
  sessionRef?: string
  facet?: string
  tag?: string
  createdBy?: string
  wait?: boolean
}

/** 任务事件（SSE / 历史接口共用结构）。 */
export interface TaskEventDto {
  seq: number
  type: string
  payload?: string | null
  truncated?: boolean
  timestamp?: string
}

/** 待审批的权限申请。 */
export interface PendingPermissionDto {
  requestId: string
  kind: string
  detail?: string
  requestTime?: string
}

/** 任务概览统计。 */
export type TaskStatsDto = Record<string, number>

// ────────────────────────────── Agent 接口 ──────────────────────────────

/** 列出已登记的 agent。 */
export function listAgents(enabledOnly = false): Promise<AgentDto[] | undefined> {
  return request<AgentDto[]>(`/api/agent-hub/agents?enabledOnly=${enabledOnly ? 'true' : 'false'}`)
}

/** 取单个 agent 详情。 */
export function getAgent(id: number): Promise<AgentDto | undefined> {
  return request<AgentDto>(`/api/agent-hub/agents/${id}`)
}

/** 新增 agent。 */
export function createAgent(body: AgentSaveRequest): Promise<AgentDto | undefined> {
  return request<AgentDto>('/api/agent-hub/agents', { method: 'POST', body: JSON.stringify(body) })
}

/** 更新 agent。 */
export function updateAgent(id: number, body: AgentSaveRequest): Promise<AgentDto | undefined> {
  return request<AgentDto>(`/api/agent-hub/agents/${id}`, { method: 'PUT', body: JSON.stringify(body) })
}

/** 删除 agent（级联删除其交互口）。 */
export function deleteAgent(id: number): Promise<void> {
  return request<void>(`/api/agent-hub/agents/${id}`, { method: 'DELETE' })
}

/**
 * 设置授信（A+C 模型的「授信自动放行」）。
 * 铁律：授信时必须给范围（scopes 非空），否则后端拒绝——空范围授信等于放开一切。
 */
export function setTrust(id: number, trusted: boolean, scopes: string[]): Promise<AgentDto | undefined> {
  return request<AgentDto>(`/api/agent-hub/agents/${id}/trust`, {
    method: 'POST',
    body: JSON.stringify({ trusted, scopes }),
  })
}

/** 扫描本机已安装的其它 agent（不自动登记）。 */
export function discoverAgents(): Promise<DiscoveredAgentDto[] | undefined> {
  return request<DiscoveredAgentDto[]>('/api/agent-hub/agents/discover')
}

/** 探测某个 agent（存在性 / 版本 / profile 断言）。 */
export function probeAgent(id: number): Promise<ProbeResultDto | undefined> {
  return request<ProbeResultDto>(`/api/agent-hub/agents/${id}/probe`, { method: 'POST' })
}

// ────────────────────────────── 任务接口 ──────────────────────────────

/** 任务列表。 */
export function listTasks(status?: string, limit = 50): Promise<TaskDto[] | undefined> {
  const qs = new URLSearchParams()
  if (status) qs.set('status', status)
  qs.set('limit', String(limit))
  return request<TaskDto[]>(`/api/agent-hub/tasks?${qs.toString()}`)
}

/** 任务概览统计（含 PendingApprovals 待审批数）。 */
export function taskStats(): Promise<TaskStatsDto | undefined> {
  return request<TaskStatsDto>('/api/agent-hub/tasks/stats')
}

/** 取任务详情。 */
export function getTask(id: number): Promise<TaskDto | undefined> {
  return request<TaskDto>(`/api/agent-hub/tasks/${id}`)
}

/** 创建并（后台）执行委派任务。 */
export function createTask(body: DelegationRequest): Promise<TaskDto | undefined> {
  return request<TaskDto>('/api/agent-hub/tasks', { method: 'POST', body: JSON.stringify(body) })
}

/** 取消任务（终止外部 agent 进程树）。 */
export function cancelTask(id: number): Promise<void> {
  return request<void>(`/api/agent-hub/tasks/${id}/cancel`, { method: 'POST' })
}

/** 取任务历史事件（全量或 fromSeq 之后）。 */
export function listTaskEvents(id: number, fromSeq = 0): Promise<TaskEventDto[] | undefined> {
  return request<{ taskId: number; fromSeq: number; events: TaskEventDto[] }>(
    `/api/agent-hub/tasks/${id}/events?fromSeq=${fromSeq}`,
  ).then((r) => r?.events)
}

/** 取任务当前待审批的权限申请。 */
export function listPendingPermissions(id: number): Promise<PendingPermissionDto[] | undefined> {
  return request<PendingPermissionDto[]>(`/api/agent-hub/tasks/${id}/permissions`)
}

/** 答复一个待审批的权限申请（allowed=true 仅本次放行，不记忆）。 */
export function resolvePermission(
  taskId: number,
  requestId: string,
  allowed: boolean,
  note?: string,
): Promise<void> {
  return request<void>(`/api/agent-hub/tasks/${taskId}/permissions/${encodeURIComponent(requestId)}/resolve`, {
    method: 'POST',
    body: JSON.stringify({ allowed, note }),
  })
}

// ────────────────────────────── SSE 订阅 ──────────────────────────────

/**
 * 订阅任务事件流（SSE）。
 *
 * 铁律：不用 EventSource —— 它不能带 Authorization 头，而后端要求 Bearer 鉴权。
 * 这里用 fetch + ReadableStream 手工解析 SSE 帧，与宿主 SSE 客户端同一套路。
 *
 * 支持 G1 断线续读：调用方把已收到的最大 seq 传进来，重连时自动补历史、不丢事件。
 *
 * @param taskId 任务主键
 * @param fromSeq 起始序号（含），0 表示从头
 * @param onEvent 收到一条归一化事件时回调
 * @param onDone 收到服务端 done 帧（任务进入终态）时回调
 * @param signal 用于外部中止（组件卸载 / 切换任务）
 * @returns 停止函数
 */
export function streamTaskEvents(
  taskId: number,
  fromSeq: number,
  onEvent: (evt: TaskEventDto) => void,
  onDone?: (payload: { taskId: number; status: string; lastSeq: number }) => void,
  signal?: AbortSignal,
): () => void {
  const ctrl = new AbortController()
  // 外部 signal 与内部 ctrl 联动：任一方中止都停流
  const onAbort = () => ctrl.abort()
  signal?.addEventListener('abort', onAbort)

  void (async () => {
    try {
      const res = await fetch(
        `/api/agent-hub/tasks/${taskId}/stream?fromSeq=${fromSeq}`,
        { headers: buildHeaders(), signal: ctrl.signal },
      )
      if (!res.ok || !res.body) throw new Error(`事件流连接失败（HTTP ${res.status}）`)

      const reader = res.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''

      for (;;) {
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })

        // SSE 以空行分隔帧；保留最后一段（可能不完整）继续拼接
        const frames = buffer.split('\n\n')
        buffer = frames.pop() ?? ''

        for (const frame of frames) {
          const evtName = /^event:\s*(.+)$/m.exec(frame)?.[1]?.trim() ?? ''
          const dataRaw = /^data:\s*(.+)$/m.exec(frame)?.[1]?.trim() ?? ''
          if (!dataRaw) continue

          // 心跳注释帧（: keep-alive）没有 data，前面已跳过
          if (evtName === 'done') {
            try {
              onDone?.(JSON.parse(dataRaw))
            } catch {
              onDone?.({ taskId, status: 'Unknown', lastSeq: fromSeq })
            }
            return
          }

          try {
            const parsed = JSON.parse(dataRaw) as TaskEventDto
            // 后端 WriteEventAsync 写的是 { seq, type, payload, truncated }，与事件名重复也无妨
            if (!parsed.type) parsed.type = evtName
            onEvent(parsed)
          } catch {
            // 单帧坏数据不打断整条流
          }
        }
      }
    } catch (e) {
      // 主动 abort 不算错误
      if ((e as Error).name !== 'AbortError') onDone?.({ taskId, status: 'StreamError', lastSeq: fromSeq })
    } finally {
      signal?.removeEventListener('abort', onAbort)
    }
  })()

  return () => ctrl.abort()
}

/** 读取本插件当前运行版本（宿主 /api/plugin 清单，request 已解包 data），用于界面展示自身版本。 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const list = await request<Array<{ id: string; version: string }>>('/api/plugin', { method: 'GET' })
  const mine = (list ?? []).find((p) => p?.id === pluginId)
  return mine?.version ?? ''
}
