/**
 * 多语句结果标签模型（FR-16~19 / BR-15~19）。
 * 每条语句一个 ResultSet，各自持有 status/结果/错误/分页；纯函数便于单测。
 */
import type { QueryResponse, ResultErrorState } from './types'
import { clampPageSize } from './pager'

export type ResultStatus = 'pending' | 'ok' | 'error' | 'awaiting_approval' | 'skipped'

export interface ResultSet {
  /** 该结果对应的单条语句原文 */
  sql: string
  status: ResultStatus
  /** status=ok 时的响应 */
  result: QueryResponse | null
  /** 失败/拒绝时的红条 */
  error: ResultErrorState | null
  /** skipped 时的原因文案（如「未执行（第 2 条失败）」） */
  skipReason?: string
  page: number
  pageSize: number
  running: boolean
}

export const DEFAULT_PAGE_SIZE = 50

/** 由切分后的语句建结果集（全部 pending，默认第 1 页） */
export function buildResultSets(statements: string[], pageSize = DEFAULT_PAGE_SIZE): ResultSet[] {
  const size = clampPageSize(pageSize)
  return statements.map(sql => ({
    sql,
    status: 'pending' as ResultStatus,
    result: null,
    error: null,
    page: 1,
    pageSize: size,
    running: false,
  }))
}

/** 从 fromIndex 起标记未执行（失败即停，BR-17） */
export function markSkippedFrom(sets: ResultSet[], fromIndex: number, failedIndex: number): ResultSet[] {
  const reason = `未执行（第 ${failedIndex + 1} 条失败）`
  return sets.map((s, i) => (
    i >= fromIndex && s.status === 'pending'
      ? { ...s, status: 'skipped' as ResultStatus, running: false, skipReason: reason }
      : s
  ))
}

/** 结果标签标题「结果 i」（i 从 1 起） */
export function resultLabel(index: number): string {
  return `结果 ${index + 1}`
}

export type ResultDot = 'ok' | 'error' | 'awaiting' | 'skipped' | 'pending'

/** 状态点（视觉规格：成功/失败/待审批/未执行/待执行） */
export function resultDot(set: ResultSet): ResultDot {
  switch (set.status) {
    case 'ok': return 'ok'
    case 'error': return 'error'
    case 'awaiting_approval': return 'awaiting'
    case 'skipped': return 'skipped'
    default: return 'pending'
  }
}

/** 状态点 tooltip */
export function resultDotTitle(set: ResultSet, index: number): string {
  switch (set.status) {
    case 'ok': return `第 ${index + 1} 条：成功`
    case 'error': return set.error?.message ? `第 ${index + 1} 条：失败 · ${set.error.message}` : `第 ${index + 1} 条：失败`
    case 'awaiting_approval': return `第 ${index + 1} 条：待审批`
    case 'skipped': return set.skipReason ?? `第 ${index + 1} 条：未执行`
    default: return `第 ${index + 1} 条：待执行`
  }
}

/** 取活动结果；越界回退 null */
export function activeResult(sets: ResultSet[], index: number): ResultSet | null {
  if (index < 0 || index >= sets.length) return null
  return sets[index]
}

/** 状态栏「N 行 · X ms」（FR-23）；写语句取 affectedRows */
export function statsLabel(set: ResultSet | null): string {
  if (!set || set.status !== 'ok' || !set.result) return ''
  const r = set.result
  const parts: string[] = []
  if (r.total !== undefined) parts.push(`${r.total} 行`)
  else if (r.rows?.length) parts.push(`${r.rows.length} 行`)
  else if (r.affectedRows !== undefined) parts.push(`影响 ${r.affectedRows} 行`)
  const ms = r.elapsedMs ?? set.result.elapsedMs
  if (ms !== undefined) parts.push(`${ms} ms`)
  return parts.join(' · ')
}
