/** DbAdmin HTTP 客户端 —— 路径严格按 docs/03 §3（前缀 /api/db-admin）；响应经 normalize 归一 */
import { apiDelete, apiGet, apiPost, apiPut } from './http'
import {
  listOf, normApproval, normAudit, normConnection, normHistory, normProvider, normQuery, normTable,
  normTableDetail, normTest,
} from './normalize'
import type { DbConnectionInput, PermissionMode, QueryRequest, ResolveApprovalRequest } from './types'

export const PLUGIN_ID = 'db-admin'
const BASE = '/api/db-admin'
const enc = encodeURIComponent
type Raw = Record<string, unknown>
const obj = (d: unknown): Raw => (d && typeof d === 'object' ? (d as Raw) : {})

export const dbAdminApi = {
  providers: async () => listOf(await apiGet(`${BASE}/providers`), normProvider),
  connections: async () => listOf(await apiGet(`${BASE}/connections`), normConnection),
  createConnection: async (body: DbConnectionInput) => normConnection(obj(await apiPost(`${BASE}/connections`, body))),
  updateConnection: async (id: string, body: DbConnectionInput) =>
    normConnection(obj(await apiPut(`${BASE}/connections/${enc(id)}`, body))),
  deleteConnection: (id: string) => apiDelete<unknown>(`${BASE}/connections/${enc(id)}`),
  testConnection: async (body: DbConnectionInput & { id?: string }) =>
    normTest(obj(await apiPost(`${BASE}/connections/test`, body))),
  tables: async (id: string) => listOf(await apiGet(`${BASE}/connections/${enc(id)}/tables`), normTable),
  tableDetail: async (id: string, table: string) =>
    normTableDetail(obj(await apiGet(`${BASE}/connections/${enc(id)}/tables/${enc(table)}`))),
  query: async (body: QueryRequest) => normQuery(obj(await apiPost(`${BASE}/query`, body))),
  approvals: async (status = 'pending') => listOf(await apiGet(`${BASE}/approvals?status=${enc(status)}`), normApproval),
  resolveApproval: async (id: string, body: ResolveApprovalRequest) =>
    normQuery(obj(await apiPost(`${BASE}/approvals/${enc(id)}/resolve`, body))),
  history: async (connectionId?: string, page = 1, pageSize = 50) =>
    listOf(await apiGet(`${BASE}/history?page=${page}&pageSize=${pageSize}${connectionId ? `&connectionId=${enc(connectionId)}` : ''}`), normHistory),
  audit: async (kind?: string, page = 1, pageSize = 100) =>
    listOf(await apiGet(`${BASE}/audit?page=${page}&pageSize=${pageSize}${kind ? `&kind=${enc(kind)}` : ''}`), normAudit),
  getPermissionMode: () => apiGet<{ mode: PermissionMode }>(`${BASE}/permission-mode`),
  setPermissionMode: (mode: PermissionMode) => apiPut<{ mode: PermissionMode }>(`${BASE}/permission-mode`, { mode }),
}

export async function fetchPluginVersion(pluginId = PLUGIN_ID): Promise<string> {
  const data = await apiGet<{ id?: string; version?: string }[]>('/api/plugin')
  const list = Array.isArray(data) ? data : []
  return list.find(p => p?.id === pluginId)?.version ?? '未知版本'
}
