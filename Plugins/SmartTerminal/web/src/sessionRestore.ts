/**
 * S-R 刷新恢复：sessionStorage 持久化标签列表 / 连接摘要（无密码）/ 每标签输出缓冲。
 * 上限：每标签最近 1000 行 或约 100KB UTF-8（先到先截）。
 * 密码永不写入任何存储。
 */
import type { CommandRecord, OutputChunk, PermissionMode, TerminalSession } from './types'

export const SESSION_RESTORE_KEY = 'forge.self.smart-terminal.session-restore.v1'
export const MAX_LINES_PER_TAB = 1000
export const MAX_BYTES_PER_TAB = 100 * 1024

export interface RestoredConnectionSummary {
  profile?: string
  launchType?: string
  host?: string
  port?: number
  user?: string
  cwd?: string
  shellId?: string
  shellDisplayName?: string
  title?: string
  authMethod?: string
}

export interface RestoredTab {
  sessionId: string
  title: string
  shellId: string
  shellDisplayName: string
  cwd: string
  permissionMode: PermissionMode | number | string
  status: string
  createdAt?: string
  updatedAt?: string
  commandCount?: number
  launchType?: string
  remoteHost?: string
  remoteUser?: string
  connection?: RestoredConnectionSummary
}

export interface SessionRestoreSnapshot {
  version: 1
  savedAt: string
  activeId: string
  tabs: RestoredTab[]
  chunksBySession: Record<string, OutputChunk[]>
  commandsBySession: Record<string, CommandRecord[]>
  nextIndexBySession: Record<string, number>
}

function storage(): Storage | null {
  try {
    if (typeof sessionStorage === 'undefined') return null
    return sessionStorage
  } catch {
    return null
  }
}

function utf8Bytes(s: string): number {
  try {
    return new TextEncoder().encode(s).length
  } catch {
    return s.length
  }
}

/** 先到先截：保留最近行，同时受行数与字节上限约束 */
export function trimChunks(chunks: OutputChunk[]): OutputChunk[] {
  if (!chunks?.length) return []
  let list = chunks.length > MAX_LINES_PER_TAB ? chunks.slice(-MAX_LINES_PER_TAB) : [...chunks]
  let total = 0
  for (let i = list.length - 1; i >= 0; i--) {
    total += utf8Bytes(list[i]?.text ?? '')
    if (total > MAX_BYTES_PER_TAB) {
      list = list.slice(i + 1)
      break
    }
  }
  return list
}

function stripPasswordFields(obj: unknown): unknown {
  if (obj == null || typeof obj !== 'object') return obj
  if (Array.isArray(obj)) return obj.map(stripPasswordFields)
  const out: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
    const lower = k.toLowerCase()
    if (
      lower === 'password'
      || lower === 'pwd'
      || lower === 'passwd'
      || lower === 'ssh_password'
      || lower === 'interactivepassword'
    ) {
      continue
    }
    out[k] = stripPasswordFields(v)
  }
  return out
}

export function tabFromSession(
  s: TerminalSession,
  connection?: RestoredConnectionSummary,
): RestoredTab {
  return {
    sessionId: s.sessionId,
    title: s.title,
    shellId: s.shellId,
    shellDisplayName: s.shellDisplayName,
    cwd: s.cwd,
    permissionMode: s.permissionMode,
    status: s.status,
    createdAt: s.createdAt,
    updatedAt: s.updatedAt,
    commandCount: s.commandCount,
    launchType: s.launchType as string | undefined,
    remoteHost: s.remoteHost,
    remoteUser: s.remoteUser,
    connection: connection
      ? { ...connection }
      : {
          profile: (s.launchType as string) || 'local',
          launchType: s.launchType as string | undefined,
          host: s.remoteHost,
          user: s.remoteUser,
          cwd: s.cwd,
          shellId: s.shellId,
          shellDisplayName: s.shellDisplayName,
          title: s.title,
        },
  }
}

export function buildSnapshot(input: {
  activeId: string
  sessions: TerminalSession[]
  connectionsBySession?: Record<string, RestoredConnectionSummary>
  chunksBySession: Record<string, OutputChunk[]>
  commandsBySession: Record<string, CommandRecord[]>
  nextIndexBySession: Record<string, number>
}): SessionRestoreSnapshot {
  const tabs = input.sessions
    .filter(s => s.status !== 'closed')
    .map(s => tabFromSession(s, input.connectionsBySession?.[s.sessionId]))

  const chunksBySession: Record<string, OutputChunk[]> = {}
  const commandsBySession: Record<string, CommandRecord[]> = {}
  const nextIndexBySession: Record<string, number> = {}

  for (const t of tabs) {
    const id = t.sessionId
    chunksBySession[id] = trimChunks(input.chunksBySession[id] ?? [])
    commandsBySession[id] = [...(input.commandsBySession[id] ?? [])].slice(-200)
    nextIndexBySession[id] = input.nextIndexBySession[id] ?? 0
  }

  return {
    version: 1,
    savedAt: new Date().toISOString(),
    activeId: input.activeId,
    tabs,
    chunksBySession,
    commandsBySession,
    nextIndexBySession,
  }
}

export function saveSnapshot(snap: SessionRestoreSnapshot): void {
  const store = storage()
  if (!store) return
  try {
    const safe = stripPasswordFields(snap) as SessionRestoreSnapshot
    store.setItem(SESSION_RESTORE_KEY, JSON.stringify(safe))
  } catch {
    // quota / private mode — 静默失败，不影响主流程
  }
}

export function loadSnapshot(): SessionRestoreSnapshot | null {
  const store = storage()
  if (!store) return null
  try {
    const raw = store.getItem(SESSION_RESTORE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as SessionRestoreSnapshot
    if (!parsed || parsed.version !== 1 || !Array.isArray(parsed.tabs)) return null
    // 防御：即便历史脏数据也不把 password 带进内存之外的展示路径
    return stripPasswordFields(parsed) as SessionRestoreSnapshot
  } catch {
    return null
  }
}

export function clearSnapshot(): void {
  storage()?.removeItem(SESSION_RESTORE_KEY)
}

/** 关闭某标签后更新快照（焚毁该标签缓冲） */
export function removeTabFromSnapshot(sessionId: string): void {
  const snap = loadSnapshot()
  if (!snap) return
  snap.tabs = snap.tabs.filter(t => t.sessionId !== sessionId)
  delete snap.chunksBySession[sessionId]
  delete snap.commandsBySession[sessionId]
  delete snap.nextIndexBySession[sessionId]
  if (snap.activeId === sessionId) {
    snap.activeId = snap.tabs[0]?.sessionId ?? ''
  }
  if (!snap.tabs.length) {
    clearSnapshot()
    return
  }
  saveSnapshot(snap)
}

export function restoredTabToSession(t: RestoredTab): TerminalSession {
  return {
    sessionId: t.sessionId,
    title: t.title,
    shellId: t.shellId,
    shellDisplayName: t.shellDisplayName,
    cwd: t.cwd,
    permissionMode: t.permissionMode as PermissionMode,
    status: t.status === 'closed' ? 'idle' : t.status,
    createdAt: t.createdAt ?? new Date().toISOString(),
    updatedAt: t.updatedAt ?? new Date().toISOString(),
    commandCount: t.commandCount ?? 0,
    launchType: t.launchType,
    remoteHost: t.remoteHost ?? t.connection?.host,
    remoteUser: t.remoteUser ?? t.connection?.user,
  }
}
