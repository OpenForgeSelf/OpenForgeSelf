/**
 * MCP 中心插件极简 HTTP 封装（模板继承）。
 *
 * 插件一律直连后端 HTTP 接口，不 import 宿主模块（插件是独立预编译产物）。
 * 认证：从 localStorage 读取宿主写入的 token（键名与宿主 services/request.ts 一致），
 * 有则带 Bearer 头。宿主未启用鉴权的接口即使无 token 也能正常访问。
 */

/** 宿主写入 token 的 localStorage 键名（与 ForgeSelf.Web/src/services/request.ts 保持一致）。 */
const TOKEN_KEY = 'forge_api_token'

/** 接口返回的标准包裹结构。 */
export interface ApiEnvelope<T> {
  code?: number
  message?: string
  data?: T
}

/**
 * 统一的请求实现：附加认证头、校验状态码、自适应解包响应结构。
 * 后端两种信封：a) { success, message, data }（ApiResponse.Ok 序列化）
 *              b) { code, message, data }（部分较老控制器）
 * 共同点 = 含 `data` 且含标记字段（success 或 code）→ 解包 data；否则原样返回。
 */
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
      // 响应体非 JSON 时忽略，只用状态码兜底
    }
    throw new Error(`请求失败(${res.status}): ${detail || res.statusText || '未知错误'}`)
  }
  if (res.status === 204) return undefined
  const json = (await res.json().catch(() => undefined)) as unknown
  if (json === undefined) return undefined
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) {
      return (json as ApiEnvelope<T>).data
    }
  }
  return json as T
}

/** GET 请求并返回 data 部分。 */
export function apiGet<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'GET' })
}

/** POST 请求并返回 data 部分。 */
export function apiPost<T>(path: string, body?: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })
}

/** PUT 请求并返回 data 部分。 */
export function apiPut<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
}

/** DELETE 请求并返回 data 部分（204 无内容）。 */
export function apiDelete<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'DELETE' })
}

/* ------------------------------------------------------------------ */
/* 插件版本（铁律 13：根视图标题旁必须展示自身版本号）                  */
/* ------------------------------------------------------------------ */

/** 宿主 GET /api/plugin 的单条插件清单。 */
export interface PluginManifest {
  id: string
  name: string
  version: string
  [key: string]: unknown
}

/**
 * 拉取本插件版本：GET /api/plugin → 解包 .data → 按 id 过滤。
 * 注意响应是 { data:[...], code, message, success } 包装，不是裸数组。
 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  try {
    const data = await apiGet<PluginManifest[]>('/api/plugin')
    const item = data?.find((p) => p.id === pluginId)
    return item?.version ?? ''
  } catch {
    return ''
  }
}
