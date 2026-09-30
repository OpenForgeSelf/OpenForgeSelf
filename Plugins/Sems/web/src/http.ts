/**
 * 插件界面用的极简 HTTP 封装（与 AIAgent 插件一致）。
 * 插件是独立构建的产物，不从宿主 `@/services` 复用时，直连后端 HTTP 接口。
 * 认证：从 localStorage 读宿主写入的 token（键名与宿主 services/request.ts 一致）。
 */
import type {
  BrowseResp,
  PluginRow,
  ProjectInfo,
  RegisterProjectReq,
  RegisterResp,
} from './types'

/** 宿主写入 token 的 localStorage 键名（与 ForgeSelf.Web/src/services/request.ts 保持一致）。 */
const TOKEN_KEY = 'forge_api_token'

export interface ApiEnvelope<T> {
  code?: number
  message?: string
  data?: T
}

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T | undefined> {
  return request<T>(path, { ...init, method: 'GET' })
}

export async function apiPost<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'POST', body: JSON.stringify(body) })
}

export async function apiPut<T>(path: string, body: unknown): Promise<T | undefined> {
  return request<T>(path, { method: 'PUT', body: JSON.stringify(body) })
}

export async function apiDelete<T>(path: string): Promise<T | undefined> {
  return request<T>(path, { method: 'DELETE' })
}

export function withQuery(path: string, query: Record<string, string | number | null | undefined>): string {
  const qs = Object.entries(query)
    .filter(([, v]) => v != null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
    .join('&')
  return qs ? `${path}?${qs}` : path
}

/* ------------------------------------------------------------------ *
 * 项目工作区操作（与后端 IProjectService 一一对应；界面不直连宿主接缝）
 * ------------------------------------------------------------------ */

/** 插件内部登记一个项目（来源 manual）。返回登记后的项目档案。 */
export async function registerProject(req: RegisterProjectReq): Promise<ProjectInfo> {
  const resp = await apiPost<RegisterResp>('/api/projects', req)
  if (!resp?.project) throw new Error('登记失败：后端未返回项目档案')
  return resp.project
}

/** 移除项目档案（级联删命令；不触碰磁盘文件）。 */
export async function removeProject(id: number): Promise<void> {
  await apiDelete(`/api/projects/${id}`)
}

/** 目录浏览：path 省略即列举本机驱动器。 */
export async function browseDirectories(path?: string): Promise<BrowseResp> {
  const resp = await apiGet<BrowseResp>(withQuery('/api/projects/browse', { path }))
  return resp ?? { path: null, parent: null, directories: [] }
}

/**
 * 本插件版本号（铁律 13：根视图标题旁徽标）。
 * GET /api/plugin 已由 request() 解过信封，直接是插件清单数组。
 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const list = (await apiGet<PluginRow[]>('/api/plugin')) ?? []
  const mine = Array.isArray(list) ? list.find((p) => p?.id === pluginId) : undefined
  return mine?.version ?? ''
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
      const body = (await res.json()) as { message?: string; title?: string }
      detail = body?.message ?? body?.title ?? ''
    } catch {
      // 非 JSON 忽略
    }
    throw new Error(`请求失败(${res.status}): ${detail || res.statusText || '未知错误'}`)
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
