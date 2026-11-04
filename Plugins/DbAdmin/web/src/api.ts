/**
 * DbAdmin HTTP 客户端 —— 路径按 docs/03 §3（前缀 /api/db-admin），形态按 §3.1 已定契约；响应经 normalize 归一。
 * - /query 与 /resolve 返回 `{httpStatus, result}`：200 ok|rejected、202 awaiting_approval；非 2xx 抛 ApiError（body 保留）
 * - 分页 pageSize 前端先 clamp 到 [1, 500]（后端同样上限 500）
 * - S8 Agent 回显：GET /audit?kind=agent（可带 sessionId / status / source / connectionId 过滤）
 */
import { apiDelete, apiGet, apiPost, apiPostWithStatus, apiPut } from './http'
import {
  agentEchoesOf, listOf, normApproval, normAudit, normConnection, normHistory, normProvider, normQuery, normTable,
  normTableDetail, normTest,
} from './normalize'
import { clampPageSize } from './pager'
import type {
  DbConnectionInput, LogFilter, PermissionMode, QueryRequest, QueryResponse, ResolveApprovalRequest,
} from './types'

export const PLUGIN_ID = 'db-admin'
const BASE = '/api/db-admin'
const enc = encodeURIComponent
type Raw = Record<string, unknown>
const obj = (d: unknown): Raw => (d && typeof d === 'object' ? (d as Raw) : {})

/** LogFilter → querystring（空值跳过；pageSize clamp ≤ 500） */
export function logQuery(f: LogFilter = {}): string {
  const q: [string, string][] = [
    ['page', String(Math.max(1, Math.floor(f.page ?? 1)))],
    ['pageSize', String(clampPageSize(f.pageSize ?? 50))],
  ]
  for (const k of ['kind', 'source', 'sessionId', 'status', 'connectionId'] as const) {
    const v = f[k]
    if (v) q.push([k, v])
  }
  return q.map(([k, v]) => `${k}=${enc(v)}`).join('&')
}

/** 测连请求体：后端 ConnectionInput.Id 为 Int32? —— 仅用户连接（数字串）才带 id */
export function testBody(body: DbConnectionInput, editingId?: string | null): DbConnectionInput & { id?: number } {
  const n = editingId != null && /^\d+$/.test(editingId) ? Number(editingId) : undefined
  return n === undefined ? { ...body } : { ...body, id: n }
}

export const dbAdminApi = {
  providers: async () => listOf(await apiGet(`${BASE}/providers`), normProvider),
  connections: async () => listOf(await apiGet(`${BASE}/connections`), normConnection),
  createConnection: async (body: DbConnectionInput) => normConnection(obj(await apiPost(`${BASE}/connections`, body))),
  updateConnection: async (id: string, body: DbConnectionInput) =>
    normConnection(obj(await apiPut(`${BASE}/connections/${enc(id)}`, body))),
  deleteConnection: (id: string) => apiDelete<unknown>(`${BASE}/connections/${enc(id)}`),
  /** 恒 200：{ok, code, message, serverVersion, elapsedMs} */
  testConnection: async (body: DbConnectionInput, editingId?: string | null) =>
    normTest(obj(await apiPost(`${BASE}/connections/test`, testBody(body, editingId)))),
  tables: async (id: string) => listOf(await apiGet(`${BASE}/connections/${enc(id)}/tables`), normTable),
  tableDetail: async (id: string, table: string) =>
    normTableDetail(obj(await apiGet(`${BASE}/connections/${enc(id)}/tables/${enc(table)}`))),
  query: async (body: QueryRequest): Promise<QueryResponse> => {
    const r = await apiPostWithStatus(`${BASE}/query`, { ...body, page: Math.max(1, body.page), pageSize: clampPageSize(body.pageSize) })
    return normQuery(obj(r.data), r.status)
  },
  approvals: async (status = 'pending') => listOf(await apiGet(`${BASE}/approvals?status=${enc(status)}`), normApproval),
  /** body {decision, confirmDestructive}；破坏性未确认 → 409 confirmation_required（ApiError） */
  resolveApproval: async (id: string, body: ResolveApprovalRequest): Promise<QueryResponse> => {
    const r = await apiPostWithStatus(`${BASE}/approvals/${enc(id)}/resolve`, body)
    return normQuery(obj(r.data), r.status)
  },
  history: async (f: LogFilter = {}) => listOf(await apiGet(`${BASE}/history?${logQuery(f)}`), normHistory),
  audit: async (f: LogFilter = {}) => listOf(await apiGet(`${BASE}/audit?${logQuery({ pageSize: 100, ...f })}`), normAudit),
  /** S8：§3.1 ④ —— 审计 kind=agent */
  agentEchoes: async (f: Omit<LogFilter, 'kind'> = {}) =>
    agentEchoesOf(await apiGet(`${BASE}/audit?${logQuery({ pageSize: 50, ...f, kind: 'agent' })}`)),
  getPermissionMode: () => apiGet<{ mode: PermissionMode; default?: PermissionMode }>(`${BASE}/permission-mode`),
  setPermissionMode: (mode: PermissionMode) =>
    apiPut<{ mode: PermissionMode; default?: PermissionMode }>(`${BASE}/permission-mode`, { mode }),
}

export async function fetchPluginVersion(pluginId = PLUGIN_ID): Promise<string> {
  const data = await apiGet<{ id?: string; version?: string }[]>('/api/plugin')
  const list = Array.isArray(data) ? data : []
  return list.find(p => p?.id === pluginId)?.version ?? '未知版本'
}
