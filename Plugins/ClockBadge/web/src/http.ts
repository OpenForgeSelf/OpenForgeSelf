/**
 * 插件界面用的极简 HTTP 封装（照抄 QuickLinks/web/src/http.ts 同款形态）。
 *
 * 为什么不复用宿主的 `@/services/request`：`@/` 是宿主的路径别名，插件是独立构建的
 * 预编译产物，运行时解析不到该别名；插件一律直连后端 HTTP 接口。
 *
 * 认证：从 localStorage 读取宿主写入的 token（键名 `forge_api_token`），有则带 Bearer 头。
 */

/** 宿主写入 token 的 localStorage 键名（与 ForgeSelf.Web/src/services/request.ts 一致）。 */
const TOKEN_KEY = 'forge_api_token'

/** 接口返回的标准信封结构。 */
export interface ApiEnvelope<T> {
  code?: number
  message?: string
  data?: T
}

/** GET /api/clock-badge/now 的响应体（本插件自定义裸对象，无信封包裹）。 */
export interface ClockNowDto {
  time: string
  timestamp: number
}

/**
 * 统一请求实现：附加认证头、校验状态码、自适应解包响应结构。
 *
 * 解包策略（同 QuickLinks/http.ts）：若响应体是对象且同时具备 `code` 与 (`data` 或 `message`)，
 * 则解包 `data`；否则原样返回（裸对象）。这样对两种后端契约都能工作。
 */
async function request<T>(path: string, init: RequestInit): Promise<T> {
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

  const json = (await res.json()) as unknown
  if (json !== null && typeof json === 'object' && !Array.isArray(json)) {
    const o = json as Record<string, unknown>
    if ('data' in o && ('success' in o || 'code' in o)) {
      return (json as ApiEnvelope<T>).data as T
    }
  }
  return json as T
}

/** GET 请求：返回解包后的领域对象。 */
export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  return request<T>(path, { ...init, method: 'GET' })
}
