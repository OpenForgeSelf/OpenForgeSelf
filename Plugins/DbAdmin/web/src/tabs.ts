/**
 * 多查询标签纯逻辑（FR-1~6 / BR-1~6 / FR-11）。
 * 标签 key：表标签 `t:<connectionId>:<table>`（同表天然去重），自由标签 `m:<connectionId>:<seq>`。
 * 只做数据操作，不碰网络/存储，便于单测。
 */
import type { ResultSet } from './results'

export const MAX_TABS = 12
export const NEW_QUERY_TITLE = '新查询'

/** 批量执行暂停队列（审批期间保留；刷新即丢弃，BR-18） */
export interface BatchState {
  tabKey: string
  /** 本次批量涉及的语句（用于续跑） */
  statements: string[]
  /** 下一条待执行下标 */
  nextIndex: number
  /** 触发暂停的审批 id */
  approvalId?: string
}

export interface QueryTab {
  key: string
  connectionId: string
  /** 表标签的源表名；自由标签无 */
  table?: string
  sql: string
  /** 多语句结果（空 = 未执行过） */
  results: ResultSet[]
  activeResultIndex: number
  /** 最近一次实际发送的完整文本（分页/翻页复用） */
  lastSql: string
  running: boolean
  /** 递增的运行序号：关闭标签后迟到响应按标签存在性丢弃（BR-7） */
  runSeq: number
  batch?: BatchState
}

export function tableTabKey(connId: string, table: string): string {
  return `t:${connId}:${table}`
}

export function manualTabKey(connId: string, seq: number): string {
  return `m:${connId}:${seq}`
}

/** 表标签标题 = 表名原文（不加「表」字）；自由标签 = 「新查询」 */
export function tabTitle(tab: Pick<QueryTab, 'table'>): string {
  return tab.table ?? NEW_QUERY_TITLE
}

/** 悬浮提示：连接名 · 表名（同名表可区分） */
export function tabTooltip(tab: Pick<QueryTab, 'table' | 'connectionId'>, connName: (id: string) => string): string {
  const c = connName(tab.connectionId) || tab.connectionId
  return tab.table ? `${c} · ${tab.table}` : c
}

export function createTableTab(connId: string, table: string, sql?: string): QueryTab {
  return {
    key: tableTabKey(connId, table),
    connectionId: connId,
    table,
    sql: sql ?? `SELECT * FROM ${table}`,
    results: [],
    activeResultIndex: 0,
    lastSql: '',
    running: false,
    runSeq: 0,
  }
}

export function createManualTab(connId: string, seq: number, sql = ''): QueryTab {
  return {
    key: manualTabKey(connId, seq),
    connectionId: connId,
    sql,
    results: [],
    activeResultIndex: 0,
    lastSql: '',
    running: false,
    runSeq: 0,
  }
}

export interface OpenResult {
  tabs: QueryTab[]
  key: string | null
  /** 新建了标签 */
  created: boolean
  /** 已达上限未新建 */
  full: boolean
}

/**
 * 打开/聚焦表标签（FR-2/FR-3/BR-2/BR-10）：已存在只返回 key，绝不重置 SQL 与结果。
 */
export function openTableTab(tabs: QueryTab[], connId: string, table: string): OpenResult {
  const key = tableTabKey(connId, table)
  if (tabs.some(t => t.key === key)) return { tabs, key, created: false, full: false }
  if (tabs.length >= MAX_TABS) return { tabs, key: null, created: false, full: true }
  return { tabs: [...tabs, createTableTab(connId, table)], key, created: true, full: false }
}

/** 新建自由查询标签；seq 取该连接已有自由标签的最大序号 + 1 */
export function openManualTab(tabs: QueryTab[], connId: string): OpenResult {
  if (tabs.length >= MAX_TABS) return { tabs, key: null, created: false, full: true }
  const seq = nextManualSeq(tabs, connId)
  const tab = createManualTab(connId, seq)
  return { tabs: [...tabs, tab], key: tab.key, created: true, full: false }
}

/** 已有自由标签的最大 seq + 1（避免 key 冲突，BR-1） */
export function nextManualSeq(tabs: QueryTab[], connId: string): number {
  let max = -1
  for (const t of tabs) {
    const m = /^m:.+?:(\d+)$/.exec(t.key)
    if (t.connectionId === connId && m) max = Math.max(max, Number(m[1]))
  }
  return max + 1
}

export interface CloseResult {
  tabs: QueryTab[]
  /** 关闭后的活动标签 key；无标签为 null */
  activeKey: string | null
}

/** 关闭标签：活动标签被关 → 右邻优先，无右邻取左邻（FR-6）；非活动标签被关 → 活动保持不变 */
export function closeTab(tabs: QueryTab[], key: string, activeKey: string | null): CloseResult {
  const idx = tabs.findIndex(t => t.key === key)
  if (idx < 0) return { tabs, activeKey }
  const remain = tabs.filter(t => t.key !== key)
  if (activeKey !== key) {
    return { tabs: remain, activeKey: activeKey && remain.some(t => t.key === activeKey) ? activeKey : (remain[0]?.key ?? null) }
  }
  const next = remain[idx] ?? remain[idx - 1] ?? null
  return { tabs: remain, activeKey: next?.key ?? null }
}

export function findTab(tabs: QueryTab[], key: string | null): QueryTab | null {
  if (!key) return null
  return tabs.find(t => t.key === key) ?? null
}

/** 就地替换某个标签（返回新数组，保持顺序） */
export function replaceTab(tabs: QueryTab[], key: string, patch: Partial<QueryTab>): QueryTab[] {
  return tabs.map(t => (t.key === key ? { ...t, ...patch } : t))
}
