/**
 * S-R 刷新恢复（02§5）：localStorage，总量 ≤ 64KB。
 * 恢复：连接列表（无密码/无连接串）、当前连接、每连接一份编辑器草稿。
 * 不恢复：密码、结果集、待审批弹窗（待审批从服务端重拉）。
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

export interface RestoreSnapshot {
  version: 1
  savedAt: string
  activeConnectionId: string | null
  connections: RestoredConnection[]
  drafts: Record<string, string>
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
 * 压到预算内：先丢非当前连接里最大的草稿，再截断当前草稿尾部，最后裁连接列表。
 */
export function fitToBudget(snap: RestoreSnapshot, maxBytes = RESTORE_MAX_BYTES): RestoreSnapshot {
  const s: RestoreSnapshot = { ...snap, drafts: { ...snap.drafts }, connections: [...snap.connections] }
  const size = () => byteSize(JSON.stringify(s))
  if (size() <= maxBytes) return s

  const others = Object.keys(s.drafts)
    .filter(id => id !== s.activeConnectionId)
    .sort((a, b) => s.drafts[b].length - s.drafts[a].length)
  for (const id of others) {
    delete s.drafts[id]
    if (size() <= maxBytes) return s
  }

  const act = s.activeConnectionId
  if (act && s.drafts[act]) {
    let lo = 0
    let hi = s.drafts[act].length
    const full = s.drafts[act]
    while (lo < hi) {
      const mid = Math.ceil((lo + hi) / 2)
      s.drafts[act] = full.slice(0, mid)
      if (size() <= maxBytes) lo = mid
      else hi = mid - 1
    }
    s.drafts[act] = full.slice(0, lo)
    if (size() <= maxBytes) return s
  }

  while (s.connections.length > 0 && size() > maxBytes) {
    const idx = s.connections.findIndex(c => c.id !== s.activeConnectionId)
    s.connections.splice(idx >= 0 ? idx : s.connections.length - 1, 1)
  }
  return s
}

export function buildSnapshot(
  connections: DbConnection[],
  activeConnectionId: string | null,
  drafts: Record<string, string>,
  now: () => string = () => new Date().toISOString(),
): RestoreSnapshot {
  const ids = new Set(connections.map(c => c.id))
  const kept: Record<string, string> = {}
  for (const [id, text] of Object.entries(drafts)) {
    if (ids.has(id) && text) kept[id] = text
  }
  return fitToBudget({
    version: 1,
    savedAt: now(),
    activeConnectionId: activeConnectionId && ids.has(activeConnectionId) ? activeConnectionId : null,
    connections: connections.map(toRestoredConnection),
    drafts: kept,
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

export function loadSnapshot(storage: Storage | undefined = safeStorage()): RestoreSnapshot | null {
  if (!storage) return null
  const raw = storage.getItem(RESTORE_KEY)
  if (!raw) return null
  if (byteSize(raw) > RESTORE_MAX_BYTES) {
    storage.removeItem(RESTORE_KEY)
    return null
  }
  try {
    const parsed = stripSecrets(JSON.parse(raw)) as RestoreSnapshot
    if (parsed?.version !== 1 || !Array.isArray(parsed.connections)) return null
    const drafts: Record<string, string> = {}
    for (const [k, v] of Object.entries(parsed.drafts ?? {})) if (typeof v === 'string') drafts[k] = v
    return {
      version: 1,
      savedAt: String(parsed.savedAt ?? ''),
      activeConnectionId: typeof parsed.activeConnectionId === 'string' ? parsed.activeConnectionId : null,
      connections: parsed.connections.filter(c => c && typeof c.id === 'string'),
      drafts,
    }
  } catch {
    return null
  }
}

export function restoredToConnection(r: RestoredConnection): DbConnection {
  return { ...r, provider: r.provider, password: '', connStr: '' }
}

function safeStorage(): Storage | undefined {
  try {
    return typeof localStorage === 'undefined' ? undefined : localStorage
  } catch {
    return undefined
  }
}
