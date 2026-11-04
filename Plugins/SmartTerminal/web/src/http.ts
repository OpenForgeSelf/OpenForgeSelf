const TOKEN_KEY = 'forge_api_token'

export interface ApiEnvelope<T> {
  code?: number
  message?: string
  success?: boolean
  data?: T
}

export class ApiError extends Error {
  readonly status: number
  /** 失败响应体中的 data（如 needsInteractivePassword） */
  readonly data?: unknown
  constructor(status: number, message: string, data?: unknown) {
    super(message)
    this.status = status
    this.data = data
  }
}

export async function apiGet<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'GET' })
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })
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
    let data: unknown
    try {
      const body = (await res.json()) as { message?: string; data?: unknown; success?: boolean; code?: number }
      detail = body?.message ?? ''
      data = body?.data
    } catch {
      // ignore
    }
    throw new ApiError(res.status, detail || res.statusText || '未知错误', data)
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
