/**
 * DbAdmin 插件自带 HTTP 助手（不 import 宿主模块）。
 * - 读 localStorage `forge_api_token` → Bearer
 * - 成功：拆 `{success,code,message,data}` 信封
 * - 失败：03 约定错误体 `{code, message}`，code ∈ rejected_hard | readonly_connection |
 *   driver_missing | sql_error | not_found | unauthorized；也兼容信封里 data.code
 */
const TOKEN_KEY = 'forge_api_token'

export type DbErrorCode =
  | 'rejected_hard'
  | 'readonly_connection'
  | 'driver_missing'
  | 'sql_error'
  | 'not_found'
  | 'unauthorized'

export class ApiError extends Error {
  readonly status: number
  /** 03 错误码（字符串）；HTTP 401 未带 code 时补 `unauthorized` */
  readonly code?: string
  readonly data?: unknown
  constructor(status: number, message: string, code?: string, data?: unknown) {
    super(message)
    this.status = status
    this.code = code
    this.data = data
  }
}

export function apiGet<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'GET' })
}
export function apiPost<T>(path: string, body?: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })
}
export function apiPut<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
}
export function apiDelete<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'DELETE' })
}

/** 从失败体中提取 03 的字符串 code */
export function extractErrorCode(body: unknown): string | undefined {
  if (!body || typeof body !== 'object') return undefined
  const o = body as Record<string, unknown>
  if (typeof o.code === 'string') return o.code
  const d = o.data
  if (d && typeof d === 'object' && typeof (d as Record<string, unknown>).code === 'string') {
    return (d as Record<string, unknown>).code as string
  }
  return undefined
}

async function request<T>(path: string, init: RequestInit): Promise<T | undefined> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(path, { ...init, headers })
  if (!res.ok) {
    let message = ''
    let code: string | undefined
    let data: unknown
    try {
      const body = (await res.json()) as Record<string, unknown>
      message = typeof body?.message === 'string' ? body.message : ''
      code = extractErrorCode(body)
      data = body?.data
    } catch {
      // 非 JSON 错误体
    }
    if (!code && res.status === 401) code = 'unauthorized'
    throw new ApiError(res.status, message || res.statusText || '未知错误', code, data)
  }
  if (res.status === 204) return undefined
  const text = await res.text()
  if (!text) return undefined
  const json = JSON.parse(text) as unknown
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) return o.data as T
  }
  return json as T
}
