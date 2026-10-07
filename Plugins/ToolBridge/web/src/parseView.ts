/**
 * 结果三段 → 界面行 / 空态文案的纯逻辑（不碰 DOM、不发请求 ⇒ 可单测，AC15 的文案钉子挂在这里）。
 */

import type { ParseStats, ToolResult } from './types'

export interface ResultRow {
  id: string
  tool: string
  ok: boolean
  headline: string
  lines: { label: string; value: string; raw?: boolean }[]
}

/** 后端出参里这几项是"原文段"，界面必须原样展示（不 JSON 化、不折行）。 */
const RAW_KEYS = ['content', 'stdout', 'stderr']

export function toResultRow(r: ToolResult, index: number): ResultRow {
  const lines: ResultRow['lines'] = []
  if (r.reason) lines.push({ label: 'reason', value: r.reason })
  if (r.error) lines.push({ label: 'error', value: r.error })

  const result = r.result ?? {}
  for (const key of ['path', 'cwd', 'exitCode', 'bytes', 'bytesWritten', 'count']) {
    if (result[key] !== undefined && result[key] !== null) {
      lines.push({ label: key, value: String(result[key]) })
    }
  }
  for (const key of RAW_KEYS) {
    if (typeof result[key] === 'string') {
      lines.push({ label: key, value: result[key] as string, raw: true })
    }
  }
  if (result.entries !== undefined) {
    lines.push({ label: 'entries', value: JSON.stringify(result.entries, null, 2) })
  }
  if (result.truncatedNote) lines.push({ label: 'truncatedNote', value: String(result.truncatedNote) })
  if (r.truncated) lines.push({ label: 'truncated', value: r.originalBytes ? `是（原长 ${r.originalBytes} 字节）` : '是' })

  return {
    id: `${index}-${r.tool}`,
    tool: r.tool,
    ok: !!r.ok,
    headline: r.ok ? `${r.tool} 已执行` : `${r.tool} 未执行`,
    lines,
  }
}

export type EmptyStateKey = 'need-paste' | 'no-call' | 'no-turn' | 'unauthorized' | ''

/** 空态分级（plugin-development §3.4 之 4）：不同成因给不同引导，不许一律"暂无数据"。 */
export function emptyState(input: {
  hasText: boolean
  recognized: number
  unauthorized?: boolean
  turnCount?: number
}): EmptyStateKey {
  if (input.unauthorized) return 'unauthorized'
  if (!input.hasText) return 'need-paste'
  if (input.recognized === 0) return 'no-call'
  return ''
}

export const EMPTY_STATE_TEXT: Record<Exclude<EmptyStateKey, ''>, string> = {
  'unauthorized': '未取到有效令牌（401）：请先在宿主里登录并确认 token 可用，本页所有数据都来自真实后端。',
  'need-paste': '把 AI 的回复整段粘贴到上面，再点「解析」。',
  'no-call': '这段里没认出工具调用，原因见「未解析」区；不确定格式就点上面「复制初始指令」发给 AI。',
  'no-turn': '还没有回合记录：跑一轮「解析并执行」就会出现在这里。',
}

export function statsLine(stats?: ParseStats): string {
  if (!stats) return '—'
  const parts = [
    `识别 ${stats.recognized ?? 0}`,
    stats.executed !== undefined ? `执行 ${stats.executed}` : '',
    stats.rejected !== undefined ? `被拒 ${stats.rejected}` : '',
    `未知 ${stats.unknown ?? 0}`,
    `未解析 ${stats.unparsed ?? 0}`,
    stats.durationMs !== undefined ? `耗时 ${stats.durationMs}ms` : '',
  ].filter(Boolean)
  return parts.join(' · ')
}

export function formatTime(iso: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString()
}
