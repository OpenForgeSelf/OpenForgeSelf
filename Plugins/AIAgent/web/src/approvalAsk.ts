/**
 * 会话工具审批 → 输入框上方「待你动作」卡。
 *
 * 契约（后端并行实现，前端按此调用）：
 *   GET  /api/ai-agent/approvals?sessionId=
 *   POST /api/ai-agent/approvals/{id}/decide   { approved: boolean, note?: string }
 * 只把 status=Pending 收成 AskItem；参数原文完整展示，不改写成摘要。
 */
import type { AskItem } from './composerContext'
import { withQuery } from './http'

/** 会话进行中刷新间隔。接现有 isSending，不另起聊天通道。 */
export const APPROVAL_POLL_MS = 1500

export interface ToolApproval {
  id: string
  sessionId?: string
  toolName: string
  argsJson?: string
  argsHash?: string
  riskLevel?: string
  description?: string
  status?: string
  createdAt?: string
  expiresAt?: string
}

type Risk = NonNullable<AskItem['risk']>

const RISK_KEYS: Record<string, Risk> = {
  low: 'low',
  '0': 'low',
  medium: 'medium',
  med: 'medium',
  '1': 'medium',
  high: 'high',
  '2': 'high',
  critical: 'critical',
  severe: 'critical',
  '3': 'critical',
}

/** 生成中，或屏上已有待批卡（等过期/撤销）时才打列表接口。 */
export function shouldPollApprovals(sending: boolean, pendingCount: number): boolean {
  return sending || pendingCount > 0
}

function field(row: Record<string, unknown>, name: string): unknown {
  if (row[name] != null && row[name] !== '') return row[name]
  const pascal = name.charAt(0).toUpperCase() + name.slice(1)
  const alt = row[pascal]
  return alt != null && alt !== '' ? alt : undefined
}

function asString(value: unknown): string | undefined {
  if (value == null) return undefined
  return String(value)
}

/** 列表可能是数组，或信封解开后的 { items } / { approvals }。 */
export function unwrapApprovalList(data: unknown): ToolApproval[] {
  const list = Array.isArray(data)
    ? data
    : data && typeof data === 'object'
      ? ((data as { items?: unknown }).items ?? (data as { approvals?: unknown }).approvals)
      : undefined
  if (!Array.isArray(list)) return []
  const out: ToolApproval[] = []
  for (const raw of list) {
    if (!raw || typeof raw !== 'object') continue
    const row = raw as Record<string, unknown>
    const id = field(row, 'id')
    if (id == null) continue
    out.push({
      id: String(id),
      sessionId: asString(field(row, 'sessionId')),
      toolName: asString(field(row, 'toolName')) ?? '',
      argsJson: asString(field(row, 'argsJson')),
      argsHash: asString(field(row, 'argsHash')),
      riskLevel: asString(field(row, 'riskLevel')),
      description: asString(field(row, 'description')),
      status: asString(field(row, 'status')),
      createdAt: asString(field(row, 'createdAt')),
      expiresAt: asString(field(row, 'expiresAt')),
    })
  }
  return out
}

/**
 * 完整参数。JSON 只做缩进排版，字段一个不丢；
 * 不是 JSON 就原样展示（含命令字符串），空参数明确写出来。
 */
export function formatApprovalArgs(argsJson: string | null | undefined): string {
  const raw = String(argsJson ?? '')
  const trimmed = raw.trim()
  if (!trimmed) return '（此调用没有参数）'
  try {
    return JSON.stringify(JSON.parse(trimmed) as unknown, null, 2)
  } catch {
    return raw
  }
}

export function mapRisk(level: unknown): { risk: Risk; riskLabel?: string } {
  const raw = String(level ?? '').trim()
  const key = RISK_KEYS[raw.toLowerCase()]
  if (key) return { risk: key }
  if (!raw) return { risk: 'high', riskLabel: '风险未标明' }
  return { risk: 'high', riskLabel: raw }
}

export function isPendingStatus(status: string | undefined): boolean {
  return String(status ?? '').trim().toLowerCase() === 'pending'
}

/** 只留本会话的 Pending。已批准 / 拒绝 / 过期 / 取消的不占输入框上沿。 */
export function mapPendingApprovals(
  rows: ToolApproval[] | null | undefined,
  sessionId?: string,
): AskItem[] {
  const want = sessionId?.trim()
  const items: AskItem[] = []
  for (const row of rows ?? []) {
    if (!row?.id || !isPendingStatus(row.status)) continue
    if (want && row.sessionId && row.sessionId !== want) continue
    const risk = mapRisk(row.riskLevel)
    const description = row.description?.trim()
    items.push({
      id: row.id,
      kind: 'approval',
      title: row.toolName.trim() || '工具调用',
      detail: description || undefined,
      risk: risk.risk,
      riskLabel: risk.riskLabel,
      argsText: formatApprovalArgs(row.argsJson),
    })
  }
  return items
}

export function approvalsQueryPath(sessionId: string): string {
  return withQuery('/api/ai-agent/approvals', { sessionId })
}

export function approvalDecidePath(id: string): string {
  return `/api/ai-agent/approvals/${encodeURIComponent(id)}/decide`
}

export async function fetchSessionApprovals(
  sessionId: string,
  get: (path: string) => Promise<unknown>,
): Promise<AskItem[]> {
  if (!sessionId.trim()) return []
  const data = await get(approvalsQueryPath(sessionId))
  return mapPendingApprovals(unwrapApprovalList(data), sessionId)
}

export async function submitApprovalDecision(
  id: string,
  decision: string,
  post: (path: string, body: unknown) => Promise<unknown>,
  note?: string,
): Promise<void> {
  const body: { approved: boolean; note?: string } = { approved: decision === 'approve' }
  const trimmed = note?.trim()
  if (trimmed) body.note = trimmed
  await post(approvalDecidePath(id), body)
}

export function approvalFeedback(decision: string): string {
  return decision === 'approve' ? '已批准' : '已拒绝'
}
