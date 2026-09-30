/**
 * 设计系统插件界面的 HTTP 封装（直连后端，与宿主共享数据而非共享代码）。
 *
 * 为什么不复用宿主的 `@/services/request`：`@/` 是宿主的路径别名，插件是独立构建的产物，
 * 既解析不到也不该打包第二份实现（状态隔离、行为易漂移）。
 *
 * 认证：宿主登录后把 token 写进 `localStorage['forge_api_token']`（键名与
 * `ForgeSelf.Web/src/services/request.ts` 同源），本插件设计端点全部要求
 * `ApiKeyPolicy`，所以**没 token 就一定 401**——空态必须把这一点告诉用户，
 * 不能让他们以为"插件坏了"。
 *
 * 解包：本项目后端统一信封是 `{ success, data }`（错误是 `{ success:false, error }`）。
 * 只解一层 `data`（plugin-development 铁律 15），不做启发式多层剥壳。
 */

const TOKEN_KEY = 'forge_api_token'

/** 后端返回的业务错误：保留状态码，界面按 401/403/409/400/404 分级提示。 */
export class ApiError extends Error {
  readonly status: number
  /** 400 时后端可能带令牌校验明细（diagnostics），原样透出给界面逐条展示。 */
  readonly details?: unknown

  constructor(status: number, message: string, details?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }

  /** 未鉴权（没 token / token 过期）。界面必须显示"去宿主登录"而不是空白。 */
  get isUnauthorized(): boolean {
    return this.status === 401 || this.status === 403
  }

  /** 冲突（版本号占用、名称重复、门禁未过）。 */
  get isConflict(): boolean {
    return this.status === 409
  }
}

function authHeaders(extra?: Record<string, string>): Record<string, string> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json', ...(extra ?? {}) }
  const token = readToken()
  if (token) headers.Authorization = `Bearer ${token}`
  return headers
}

/** 宿主可能把 token 包成 `{ value }` 或直接存字符串；两种都兼容。 */
function readToken(): string {
  const raw = localStorage.getItem(TOKEN_KEY) ?? ''
  if (!raw) return ''
  if (raw.startsWith('{')) {
    try {
      const parsed = JSON.parse(raw) as { value?: string; accessToken?: string; token?: string }
      return parsed.value ?? parsed.accessToken ?? parsed.token ?? ''
    } catch {
      return raw
    }
  }
  return raw
}

/** 当前是否具备调用受保护端点的凭据（界面据此显示"未登录"空态，而不是转圈）。 */
export function hasToken(): boolean {
  return readToken().length > 0
}

/** 解析失败响应体，取后端给的中文错误信息（非 JSON 的 500 原文也保留，不降级成"500 Internal Server Error"）。 */
async function toError(res: Response): Promise<ApiError> {
  const raw = await res.text()
  let message = `${res.status} ${res.statusText}`.trim()
  let details: unknown
  try {
    const body = JSON.parse(raw) as { error?: string; message?: string; title?: string; diagnostics?: unknown }
    message = body.error ?? body.message ?? body.title ?? message
    details = body.diagnostics
  } catch {
    if (raw) message = `${res.status} ${raw.slice(0, 200)}`
  }
  return new ApiError(res.status, message, details)
}

const sleep = (ms: number): Promise<void> => new Promise((r) => setTimeout(r, ms))

/**
 * 只读请求遇到宿主 SQLite 并发锁（500「code = Busy」）时退避重试。
 * 背景：宿主 DAL 并发读写偶发 SQLITE_BUSY（已记 TODO：宿主级议题），一次锁冲突不该让工作台红屏。
 * **写请求一律不重试** —— 重复落库比红屏更糟。
 */
async function fetchRead(path: string, init: RequestInit): Promise<Response> {
  for (let attempt = 0; attempt < 3; attempt++) {
    if (attempt > 0) await sleep(400 * attempt)
    const res = await fetch(path, { ...init, headers: authHeaders(init.headers as Record<string, string>) })
    if (res.ok || res.status < 500) return res
    const text = await res.clone().text()
    if (!/Busy|database is locked/i.test(text)) return res
  }
  return fetch(path, { ...init, headers: authHeaders(init.headers as Record<string, string>) })
}

/**
 * JSON 请求，自动解 `{ success, data }` 信封。
 *
 * @param path 以 / 开头的接口路径
 * @param init fetch 参数（method/body/headers）
 */
export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const isGet = (init.method ?? 'GET').toUpperCase() === 'GET'
  const res = isGet ? await fetchRead(path, init) : await fetch(path, { ...init, headers: authHeaders(init.headers as Record<string, string>) })
  if (!res.ok) throw await toError(res)
  if (res.status === 204) return undefined as T
  const json = (await res.json()) as unknown
  return unwrap<T>(json)
}

/** 只解一层信封：带 `success` 或 `code` 标记且含 `data` 才剥，裸对象原样返回。 */
function unwrap<T>(json: unknown): T {
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) return o.data as T
  }
  return json as T
}

export function get<T>(path: string): Promise<T> {
  return request<T>(path, { method: 'GET' })
}

export function post<T>(path: string, body?: unknown): Promise<T> {
  return request<T>(path, { method: 'POST', body: JSON.stringify(body ?? {}) })
}

export function put<T>(path: string, body?: unknown): Promise<T> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body ?? {}) })
}

/**
 * 导出端点是**文件下载**（text/css/json 原文件，不是信封），单独一条通道。
 * 用 text 读回给界面预览；需要落盘时用 url 直接开新窗口。
 */
export async function getExportText(path: string): Promise<string> {
  const res = await fetchRead(path, { method: 'GET', headers: { Accept: 'text/plain, application/json, text/css' } })
  if (!res.ok) throw await toError(res)
  return res.text()
}

/** 导出下载直链（宿主带不了自定义头，交给浏览器同源会话；401 时后端仍会拒绝）。 */
export function exportUrl(path: string): string {
  return path
}

/** 拼查询串：值为 null/undefined/空串时省略，避免后端把空串当筛选条件。 */
export function withQuery(base: string, query: Record<string, string | number | boolean | null | undefined>): string {
  const qs = Object.entries(query)
    .filter(([, v]) => v !== null && v !== undefined && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
    .join('&')
  return qs ? `${base}?${qs}` : base
}
