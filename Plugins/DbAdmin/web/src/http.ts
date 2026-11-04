/**
 * DbAdmin 插件自带 HTTP 助手（不 import 宿主模块）。
 * - 读 localStorage `forge_api_token` → Bearer
 * - 成功：拆 `{success,code,message,data}` 信封；`apiPostWithStatus` 额外带回 HTTP 状态（/query 的 202）
 * - 失败：§3.1 错误体 `{code, message}`（/query、resolve 失败时 body 是完整 QueryOutcome，同样带 code/message）。
 *   状态码：400 sql_error/invalid_request · 401 unauthorized · 403 · 404 not_found · 409 confirmation_required ·
 *   424 driver_missing；也兼容信封里 data.code
 */
const TOKEN_KEY = 'forge_api_token'

export type DbErrorCode =
  | 'rejected_hard'
  | 'readonly_connection'
  | 'driver_missing'
  | 'sql_error'
  | 'not_found'
  | 'unauthorized'
  | 'confirmation_required'
  | 'invalid_request'
  | 'approval_rejected'

export class ApiError extends Error {
  readonly status: number
  /** 03 错误码（字符串）；HTTP 401 未带 code 时补 `unauthorized` */
  readonly code?: string
  readonly data?: unknown
  /** 原始错误体（如 /query 的 QueryOutcome），供结果区统一渲染 */
  readonly body?: unknown
  constructor(status: number, message: string, code?: string, data?: unknown, body?: unknown) {
    super(message)
    this.status = status
    this.code = code
    this.data = data
    this.body = body
  }
}

/** 带 HTTP 状态的成功应答 */
export interface StatusReply<T> {
  status: number
  data: T | undefined
}

export function apiGet<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'GET' })
}
export function apiPost<T>(path: string, body?: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })
}
/** POST 并带回 HTTP 状态（/query：200 ok|rejected，202 awaiting_approval） */
export function apiPostWithStatus<T>(path: string, body?: unknown): Promise<StatusReply<T>> {
  return requestWithStatus<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })
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
  return (await requestWithStatus<T>(path, init)).data
}

async function requestWithStatus<T>(path: string, init: RequestInit): Promise<StatusReply<T>> {
  const token = localStorage.getItem(TOKEN_KEY)
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(path, { ...init, headers })
  if (!res.ok) {
    let message = ''
    let code: string | undefined
    let data: unknown
    let raw: unknown
    try {
      const body = (await res.json()) as Record<string, unknown>
      raw = body
      message = typeof body?.message === 'string' ? body.message : ''
      code = extractErrorCode(body)
      data = body?.data
    } catch {
      // 非 JSON 错误体
    }
    if (!code && res.status === 401) code = 'unauthorized'
    throw new ApiError(res.status, message || res.statusText || '未知错误', code, data, raw)
  }
  if (res.status === 204) return { status: 204, data: undefined }
  const text = await res.text()
  if (!text) return { status: res.status, data: undefined }
  const json = JSON.parse(text) as unknown
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) return { status: res.status, data: o.data as T }
  }
  return { status: res.status, data: json as T }
}
