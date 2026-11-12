/**
 * S-R 刷新恢复（02§5 / FR-12 / BR-8~9）：localStorage，总量 ≤ 64KB。
 * 恢复：连接列表（无密码/无连接串）、查询标签（key/连接/表/SQL）、活动标签、各连接默认表。
 * 不恢复：密码、结果集、待审批弹窗（待审批从服务端重拉）。
 * 兼容旧快照：`drafts: Record<connectionId, string>` 迁移为每个连接一个自由标签 `m:<connectionId>:0`。
 */
import type { DbConnection } from './types'

export const RESTORE_KEY = 'forge.self.db-admin.restore.v1'
export const RESTORE_MAX_BYTES = 64 * 1024

export interface RestoredConnection {
  id: string
  name: string
  provider: string
  mode: 'form' | 'connstr'
  readonly: boolean
  system?: boolean
  host?: string
  port?: number
  database?: string
  userName?: string
}

/** 恢复的标签元数据 + 该标签 SQL（结果集不恢复） */
export interface RestoredTab {
  key: string
  connectionId: string
  table?: string
  sql: string
}

export interface RestoreSnapshot {
  version: 1
  savedAt: string
  activeConnectionId: string | null
  connections: RestoredConnection[]
  /** 旧格式（早期 v1）：每连接一份草稿。读到后迁移成 tabs；新快照不再写。 */
  drafts?: Record<string, string>
  /** 查询标签（顺序 = 创建顺序） */
  tabs: RestoredTab[]
  /** 活动标签 key */
  activeTabKey: string | null
  /** 本机界面偏好：connectionId → 默认表名（trim 后为空 = 未配置） */
  defaultTables: Record<string, string>
}

const SECRET_KEYS = new Set([
  'password', 'pwd', 'passwd', 'passwordenc', 'connstr', 'connstrenc', 'connectionstring', 'secret', 'token',
])

/** 深度剥离任何密码/连接串字段（落盘前、读回后各跑一次） */
export function stripSecrets<T>(obj: T): T {
  if (obj === null || typeof obj !== 'object') return obj
  if (Array.isArray(obj)) return obj.map(v => stripSecrets(v)) as unknown as T
  const out: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
    if (SECRET_KEYS.has(k.toLowerCase())) continue
    out[k] = stripSecrets(v)
  }
  return out as T
}

export function toRestoredConnection(c: DbConnection): RestoredConnection {
  const r: RestoredConnection = {
    id: c.id,
    name: c.name,
    provider: c.provider,
    mode: c.mode,
    readonly: !!c.readonly,
  }
  if (c.system !== undefined) r.system = c.system
  if (c.host) r.host = c.host
  if (c.port) r.port = c.port
  if (c.database) r.database = c.database
  if (c.userName) r.userName = c.userName
  return r
}

export function byteSize(s: string): number {
  return new TextEncoder().encode(s).length
}

/**
 * 压到预算内：先丢非活动标签的 SQL（长的先丢），再截断活动标签 SQL 尾部，最后裁连接列表。
 */
export function fitToBudget(snap: RestoreSnapshot, maxBytes = RESTORE_MAX_BYTES): RestoreSnapshot {
  const s: RestoreSnapshot = {
    ...snap,
    tabs: snap.tabs.map(t => ({ ...t })),
    connections: [...snap.connections],
    defaultTables: { ...snap.defaultTables },
  }
  const size = () => byteSize(JSON.stringify(s))
  if (size() <= maxBytes) return s

  const others = s.tabs
    .filter(t => t.key !== s.activeTabKey && t.sql)
    .sort((a, b) => b.sql.length - a.sql.length)
  for (const t of others) {
    t.sql = ''
    if (size() <= maxBytes) return s
  }

  const act = s.tabs.find(t => t.key === s.activeTabKey)
  if (act && act.sql) {
    const full = act.sql
    let lo = 0
    let hi = full.length
    while (lo < hi) {
      const mid = Math.ceil((lo + hi) / 2)
      act.sql = full.slice(0, mid)
      if (size() <= maxBytes) lo = mid
      else hi = mid - 1
    }
    act.sql = full.slice(0, lo)
    if (size() <= maxBytes) return s
  }

  while (s.connections.length > 0 && size() > maxBytes) {
    const idx = s.connections.findIndex(c => c.id !== s.activeConnectionId)
    s.connections.splice(idx >= 0 ? idx : s.connections.length - 1, 1)
  }
  return s
}

/** 提交时清洗默认表：只保留存在的连接、trim 后非空 */
function cleanDefaultTables(defaultTables: Record<string, string>, ids: Set<string>): Record<string, string> {
  const out: Record<string, string> = {}
  for (const [id, v] of Object.entries(defaultTables ?? {})) {
    const t = String(v ?? '').trim()
    if (ids.has(id) && t) out[id] = t
  }
  return out
}

export function buildSnapshot(
  connections: DbConnection[],
  tabs: RestoredTab[],
  activeTabKey: string | null,
  defaultTables: Record<string, string>,
  now: () => string = () => new Date().toISOString(),
): RestoreSnapshot {
  const ids = new Set(connections.map(c => c.id))
  const kept = tabs
    .filter(t => ids.has(t.connectionId))
    .map(t => (t.sql ? { ...t, sql: t.sql } : { ...t, sql: '' }))
  const active = activeTabKey && kept.some(t => t.key === activeTabKey) ? activeTabKey : (kept[0]?.key ?? null)
  const activeConn = kept.find(t => t.key === active)?.connectionId ?? null
  return fitToBudget({
    version: 1,
    savedAt: now(),
    activeConnectionId: activeConn,
    connections: connections.map(toRestoredConnection),
    tabs: kept,
    activeTabKey: active,
    defaultTables: cleanDefaultTables(defaultTables, ids),
  })
}

export function saveSnapshot(snap: RestoreSnapshot, storage: Storage | undefined = safeStorage()): boolean {
  if (!storage) return false
  const clean = fitToBudget(stripSecrets(snap))
  const text = JSON.stringify(clean)
  if (byteSize(text) > RESTORE_MAX_BYTES) return false
  try {
    storage.setItem(RESTORE_KEY, text)
    return true
  } catch {
    return false
  }
}

/** 旧快照 `drafts` → 每连接一个自由标签（BR-8） */
function migrateDrafts(drafts: Record<string, string>, connectionIds: string[]): { tabs: RestoredTab[]; activeTabKey: string | null } {
  const order = connectionIds.filter(id => typeof drafts[id] === 'string')
  for (const id of Object.keys(drafts)) if (!order.includes(id)) order.push(id)
  const tabs = order.map(id => ({ key: `m:${id}:0`, connectionId: id, sql: drafts[id] ?? '' }))
  return { tabs, activeTabKey: tabs[0]?.key ?? null }
}

function normTab(v: unknown): RestoredTab | null {
  if (!v || typeof v !== 'object') return null
  const o = v as Record<string, unknown>
  if (typeof o.key !== 'string' || typeof o.connectionId !== 'string') return null
  const tab: RestoredTab = { key: o.key, connectionId: o.connectionId, sql: typeof o.sql === 'string' ? o.sql : '' }
  if (typeof o.table === 'string' && o.table) tab.table = o.table
  return tab
}

export function loadSnapshot(storage: Storage | undefined = safeStorage()): RestoreSnapshot | null {
  if (!storage) return null
  const raw = storage.getItem(RESTORE_KEY)
  if (!raw) return null
  if (byteSize(raw) > RESTORE_MAX_BYTES) {
    storage.removeItem(RESTORE_KEY)
    return null
  }
  try {
    const parsed = stripSecrets(JSON.parse(raw)) as Partial<RestoreSnapshot>
    if (parsed?.version !== 1 || !Array.isArray(parsed.connections)) return null
    const connections = parsed.connections.filter(c => c && typeof c.id === 'string') as RestoredConnection[]
    const ids = connections.map(c => c.id)

    let tabs: RestoredTab[] = []
    if (Array.isArray(parsed.tabs)) {
      tabs = parsed.tabs.map(normTab).filter((t): t is RestoredTab => !!t && ids.includes(t.connectionId))
    }
    let activeTabKey = typeof parsed.activeTabKey === 'string' ? parsed.activeTabKey : null
    if (tabs.length === 0) {
      // 旧快照迁移（BR-8）
      const drafts: Record<string, string> = {}
      for (const [k, v] of Object.entries(parsed.drafts ?? {})) if (typeof v === 'string') drafts[k] = v
      const m = migrateDrafts(drafts, ids)
      tabs = m.tabs
      activeTabKey = m.activeTabKey
    }

    const defaultTables: Record<string, string> = {}
    for (const [k, v] of Object.entries(parsed.defaultTables ?? {})) {
      if (typeof v === 'string' && v.trim() && ids.includes(k)) defaultTables[k] = v.trim()
    }

    return {
      version: 1,
      savedAt: String(parsed.savedAt ?? ''),
      activeConnectionId: typeof parsed.activeConnectionId === 'string' ? parsed.activeConnectionId : null,
      connections,
      tabs,
      activeTabKey: activeTabKey && tabs.some(t => t.key === activeTabKey) ? activeTabKey : (tabs[0]?.key ?? null),
      defaultTables,
    }
  } catch {
    return null
  }
}

export function restoredToConnection(r: RestoredConnection): DbConnection {
  return { ...r, provider: String(r.provider ?? '').toLowerCase(), password: '', connStr: '' }
}

function safeStorage(): Storage | undefined {
  try {
    return typeof localStorage === 'undefined' ? undefined : localStorage
  } catch {
    return undefined
  }
}
