/**
 * 插件界面用的 HTTP 封装。
 *
 * 不复用宿主 `@/services/request`（`@/` 别名在独立构建产物里解析不到，plugin-development 铁律 4）；
 * 认证从宿主写入的 `localStorage['forge_api_token']` 取 Bearer —— **本插件全部 `api/todos*` 端点
 * 都已加 `[Authorize("ApiKeyPolicy")]`（铁律 17）**，不带 token 就是 401，所以这里必须带。
 *
 * 失败一律抛 `ApiError`（带 HTTP 码与后端 reason 原文）：409（非法流转/需确认覆盖）与 503
 * （未装 agent-hub）在界面上要走不同的提示与后续动作，不能被糊成一句"操作失败"。
 */
import type {
  AgentStatus,
  ArtifactSet,
  DelegateResult,
  DispatchPreview,
  ExecutionDraft,
  ImportResult,
  PagedResult,
  ResolveProjectResult,
  TaskExecution,
  TodoItem,
  TodoProject,
  TodoSaveRequest
} from './types'

const TOKEN_KEY = 'forge_api_token'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface Envelope<T> {
  code?: number
  message?: string
  success?: boolean
  data?: T
}

function token(): string {
  try {
    return localStorage.getItem(TOKEN_KEY) ?? ''
  } catch {
    return ''
  }
}

function headers(withBody: boolean): HeadersInit {
  const h: Record<string, string> = { Accept: 'application/json' }
  if (withBody) h['Content-Type'] = 'application/json'
  const t = token()
  if (t) h.Authorization = `Bearer ${t}`
  return h
}

/** 发一次请求并交回封套里的 data。后端 data 可能缺省（如 DELETE/204），此时返回 undefined。 */
async function request<T>(path: string, init: RequestInit): Promise<T | undefined> {
  return (await dispatch<T>(path, init)).data
}

/**
 * 连封套的 `message` 一起取回。
 * 为什么需要它：`GET artifact-sets` 在"项目里确实没有工件目录"与"有目录但一个 NN-*.md 都没匹配上"
 * 两种情况下都返回空数组，**只有 message 分得开**。界面若自己编一句"该项目没有 docs/ai/pilot 目录"，
 * 就是把后端不知道的原因说成知道（实测：e2e 里清单为空，界面却断言"没有该目录"）。
 */
async function requestEnvelope<T>(path: string, init: RequestInit): Promise<Envelope<T>> {
  return dispatch<T>(path, init)
}

/**
 * 按方法分发：GET 直发，写请求一律排队（原因见 writeChain 注释）。
 */
async function dispatch<T>(path: string, init: RequestInit): Promise<Envelope<T>> {
  const method = (init.method ?? 'GET').toUpperCase()
  if (method === 'GET') return send<T>(path, init)

  const run = (): Promise<Envelope<T>> => send<T>(path, init)
  const next = writeChain.then(run, run)
  writeChain = next.then(() => undefined, () => undefined)
  return next
}

/**
 * 写请求串行队列（PILOT-054 实测缺陷）。
 *
 * 现象：详情面板「点即保存」在连续失焦时会连发 4 个 PUT。宿主日志证明**四个都落库了**，
 * 界面却仍显示"还缺：验收判据、验证命令"——因为响应是乱序回来的，而每个响应携带的是
 * **服务端那一刻的整行快照**；最后一次到达的可能是较早那次写的旧快照，
 * `applyUpdated` 于是把新状态盖回了旧的。
 * 修法：只串写不串读（读并发无害），写按发起顺序落地 ⇒ 最后到达的响应必然是最新状态。
 */
let writeChain: Promise<unknown> = Promise.resolve()

async function send<T>(path: string, init: RequestInit): Promise<Envelope<T>> {
  let resp: Response
  try {
    resp = await fetch(path, { ...init, headers: { ...headers(init.body != null), ...(init.headers ?? {}) } })
  } catch (e) {
    throw new ApiError(`网络请求失败：${e instanceof Error ? e.message : String(e)}`, 0)
  }

  if (resp.status === 204) return {}

  let body: Envelope<T> | undefined
  const text = await resp.text()
  if (text) {
    try {
      body = JSON.parse(text) as Envelope<T>
    } catch {
      // 非 JSON 响应（例如 SPA 回退页）：把状态码与人话原文一起带出去，便于定位"端点没注册上"
      throw new ApiError(`${resp.status} ${text.slice(0, 160)}`, resp.status)
    }
  }

  if (!resp.ok || body?.success === false) {
    throw new ApiError(body?.message || `请求失败（${resp.status}）`, resp.status || 500)
  }

  return { data: body?.data, message: body?.message, code: body?.code, success: body?.success }
}

export const apiGet = <T>(path: string): Promise<T | undefined> => request<T>(path, { method: 'GET' })
export const apiPost = <T>(path: string, body?: unknown): Promise<T | undefined> =>
  request<T>(path, { method: 'POST', body: body === undefined ? '{}' : JSON.stringify(body) })
export const apiPut = <T>(path: string, body: unknown): Promise<T | undefined> =>
  request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
export const apiDelete = <T>(path: string): Promise<T | undefined> => request<T>(path, { method: 'DELETE' })

function withQuery(path: string, params: Record<string, string | number | undefined>): string {
  const usp = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '') continue
    if (typeof v === 'number' && v === 0) continue   // 0 = 不过滤（projectId/page 都不带 0 语义）
    usp.set(k, String(v))
  }
  const q = usp.toString()
  return q ? `${path}?${q}` : path
}

const BASE = '/api/todos'

// ── 任务 ──────────────────────────────────────────────────────────────

export interface TodoQuery {
  status?: string
  stage?: string
  projectId?: number
  q?: string
  page?: number
  pageSize?: number
}

export const listTodos = (query: TodoQuery = {}) =>
  apiGet<PagedResult<TodoItem>>(withQuery(BASE, { ...query }))

export const getTodo = (id: number) => apiGet<TodoItem>(`${BASE}/${id}`)
export const createTodo = (body: TodoSaveRequest & { title: string }) => apiPost<TodoItem>(BASE, body)
export const updateTodo = (id: number, body: TodoSaveRequest) => apiPut<TodoItem>(`${BASE}/${id}`, body)
export const removeTodo = (id: number) => apiDelete(`${BASE}/${id}`)
export const completeTodo = (id: number) => apiPost<TodoItem>(`${BASE}/${id}/complete`)
export const reopenTodo = (id: number) => apiPost<TodoItem>(`${BASE}/${id}/reopen`)
export const changeStage = (id: number, stage: string, reason?: string, blockReason?: string) =>
  apiPost<TodoItem>(`${BASE}/${id}/stage`, { stage, reason, blockReason })

// ── 项目 ──────────────────────────────────────────────────────────────

export const listProjects = () => apiGet<TodoProject[]>(`${BASE}/projects`)
export const resolveProject = (path: string) =>
  apiPost<ResolveProjectResult>(`${BASE}/projects/resolve`, { path })
export const linkProject = (id: number, path: string) =>
  apiPost<TodoItem>(`${BASE}/${id}/project`, { path })
export const unlinkProject = (id: number) => apiDelete(`${BASE}/${id}/project`)

// ── 工件 ──────────────────────────────────────────────────────────────

/** 工件清单：连后端给的原因文案一起返回（空清单的"为什么空"只有后端知道）。 */
export async function listArtifactSets(projectId: number, projectPath?: string):
  Promise<{ items: ArtifactSet[]; message: string }> {
  const env = await requestEnvelope<ArtifactSet[]>(
    withQuery(`${BASE}/artifact-sets`, { projectId, projectPath }), { method: 'GET' })
  return { items: env.data ?? [], message: env.message ?? '' }
}

export const importArtifacts = (id: number, dir: string, files: string[], overwrite: boolean) =>
  apiPost<ImportResult>(`${BASE}/${id}/artifacts/import`, { dir, files, overwrite })

// ── 下发与委派 ────────────────────────────────────────────────────────

export const dispatchPreview = (id: number) => apiGet<DispatchPreview>(`${BASE}/${id}/dispatch`)
export const dispatchTask = (id: number, assignee?: string) =>
  apiPost<TodoItem>(withQuery(`${BASE}/${id}/dispatch`, { assignee }))
export const delegateToAgent = (id: number, agentId?: number, permissionMode?: string) =>
  apiPost<DelegateResult>(`${BASE}/${id}/dispatch-to-agent`, { agentId, permissionMode })
export const agentStatus = (id: number) => apiGet<AgentStatus>(`${BASE}/${id}/agent-status`)
export const recordAgentResult = (id: number) =>
  apiPost<{ ok: boolean; error?: string | null; seq: number; stage: string }>(
    `${BASE}/${id}/agent-status/record`)

// ── 执行记录 ──────────────────────────────────────────────────────────

export const listRecords = (id: number, page = 1, pageSize = 50) =>
  apiGet<PagedResult<TaskExecution>>(withQuery(`${BASE}/${id}/records`, { page, pageSize }))

export const appendRecord = (id: number, draft: ExecutionDraft) =>
  apiPost<TodoItem>(`${BASE}/${id}/records`, draft)

// ── 版本徽标（plugin-development 铁律 13）─────────────────────────────

/**
 * 取本插件在宿主里的当前版本。
 * 注意宿主 `GET /api/plugin` 的返回是 `{data:[...], code, message, success}` 封套，
 * 上面 request() 已解过一层 data，这里拿到的就是数组本身（铁律 15：别再 .data）。
 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const list = await apiGet<Array<{ id?: string; version?: string }>>('/api/plugin')
  const hit = Array.isArray(list) ? list.find(p => p?.id === pluginId) : undefined
  return hit?.version ? `v${hit.version}` : '版本未知'
}
