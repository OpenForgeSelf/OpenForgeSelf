const TOKEN_KEY = 'forge_api_token'
const BASE = '/api/agent'

function authHeaders(): Record<string, string> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) headers.Authorization = `Bearer ${token}`
  return headers
}

export async function agentRequest<T>(
  path: string,
  init: RequestInit & { json?: unknown } = {}
): Promise<T> {
  const { json, headers: extra, ...rest } = init
  const headers: Record<string, string> = { ...authHeaders(), ...(extra as Record<string, string> | undefined) }
  let body = rest.body
  if (json !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(json)
  }
  const res = await fetch(`${BASE}${path}`, { ...rest, headers, body })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(text || `${res.status} ${res.statusText}`)
  }
  if (res.status === 204) return undefined as T
  const ct = res.headers.get('content-type') ?? ''
  if (ct.includes('application/json')) return (await res.json()) as T
  return undefined as T
}

export function agentAuthHeaders(): Record<string, string> {
  return authHeaders()
}

export function agentUrl(path: string): string {
  return `${BASE}${path}`
}
