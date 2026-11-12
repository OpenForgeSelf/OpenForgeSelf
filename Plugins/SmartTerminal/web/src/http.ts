const TOKEN_KEY = 'forge_api_token'

export interface ApiEnvelope<T> {
  code?: number | string
  message?: string
  success?: boolean
  data?: T
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly data?: unknown
  readonly path: string
  constructor(status: number, message: string, data?: unknown, path?: string) {
    super(message)
    this.status = status
    this.data = data
    this.path = path ?? ''
    if (data && typeof data === 'object') {
      const o = data as Record<string, unknown>
      if (typeof o.code === 'string') this.code = o.code
    }
  }

  get needsInteractivePassword(): boolean {
    if (this.code === 'password_required') return true
    const d = this.data as Record<string, unknown> | undefined
    return d?.needsInteractivePassword === true
  }

  summary(): string {
    const verb = 'request'
    return `${this.path || verb} → ${this.status} ${this.message || '未知错误'}`
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

export async function apiPatch<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PATCH', body: JSON.stringify(body) })
}

export async function apiDelete<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'DELETE' })
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
    let message = res.statusText || '未知错误'
    let code: string | undefined
    let data: unknown
    try {
      const body = (await res.json()) as Record<string, unknown>
      message = String(body?.message ?? body?.error ?? message)
      const rawCode = body?.code ?? (body?.data as Record<string, unknown> | undefined)?.code
      if (typeof rawCode === 'string') code = rawCode
      data = body?.data ?? body
      if ((body as { needsInteractivePassword?: boolean }).needsInteractivePassword && !code) {
        code = 'password_required'
      }
    } catch {
      // ignore
    }
    throw new ApiError(res.status, message, { ...(typeof data==='object' && data ? data as object : {}), code }, path)
  }

  if (res.status === 204) return undefined
  const text = await res.text()
  if (!text) return undefined
  const json = JSON.parse(text) as unknown
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) {
      return (json as ApiEnvelope<T>).data
    }
  }
  return json as T
}
