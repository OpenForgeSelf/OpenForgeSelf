/**
 * 插件界面用的极简 HTTP 封装（形状同 Plugins/QuickLinks/web/src/http.ts）。
 *
 * 为什么不复用宿主的 `@/services/request`：`@/` 是宿主别名，插件是独立预编译产物解析不到；
 * 硬打包进去还会形成两份实现漂移（plugin-development 铁律 4）。
 * 认证：读宿主写入 localStorage 的 token（键名与宿主 services/request.ts 一致）。
 * 本插件端点全部要求鉴权（铁律 17），没 token 就是 401，界面必须把它显式呈现而不是当空数据。
 */

const TOKEN_KEY = 'forge_api_token'

export interface ApiEnvelope<T> {
  code?: number
  message?: string
  success?: boolean
  data?: T
}

/** 带状态码的错误，界面据此区分「未授权」与「后端故障」。 */
export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export async function apiGet<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'GET' })
}

export async function apiPost<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: JSON.stringify(body) })
}

export async function apiPut<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
}

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
      const body = (await res.json()) as { message?: string }
      detail = body?.message ?? ''
    } catch {
      // 非 JSON 响应体只用状态码兜底
    }
    throw new ApiError(res.status, detail || res.statusText || '未知错误')
  }

  const json = (await res.json()) as unknown
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) {
      return (json as ApiEnvelope<T>).data
    }
  }
  return json as T
}
