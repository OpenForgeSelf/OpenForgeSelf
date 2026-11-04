/**
 * 写审批 → 破坏性二次确认 编排（纯函数，可单测；UI 只负责弹窗）。
 * 02§3：写语句先审批；破坏性在「批准」之后再二次确认；二次确认取消 = 不执行、审批保持 pending。
 * 硬拒 / 只读连接写入：不弹审批，直接红条。
 */
import type { DbApproval, QueryResponse, ResolveApprovalRequest } from './types'

export type ApproveOutcome =
  | { action: 'resolve'; request: ResolveApprovalRequest }
  | { action: 'cancelled' }

/** 用户在审批弹窗点「批准」 */
export async function orchestrateApprove(
  approval: Pick<DbApproval, 'destructive'>,
  confirmDestructive: () => Promise<boolean>,
): Promise<ApproveOutcome> {
  if (!approval.destructive) return { action: 'resolve', request: { decision: 'approve' } }
  const ok = await confirmDestructive()
  if (!ok) return { action: 'cancelled' }
  return { action: 'resolve', request: { decision: 'approve', confirmDestructive: true } }
}

/** 用户点「拒绝」：无需二次确认 */
export function rejectRequest(): ResolveApprovalRequest {
  return { decision: 'reject' }
}

/** 审批类型标签：kind 只有 read/write，破坏性看 destructive（§3.1） */
export function kindLabel(kind: string | undefined, destructive?: boolean): string {
  if (destructive) return '写 · 破坏性'
  return kind === 'read' ? '读' : '写'
}

/** query 响应 → UI 动作 */
export type QueryUiAction = 'show_result' | 'open_approval' | 'red_bar'

export function classifyQueryResponse(r: QueryResponse): QueryUiAction {
  if (r.status === 'awaiting_approval') return 'open_approval'
  if (r.status === 'rejected' || r.status === 'error') return 'red_bar'
  return 'show_result'
}

/** 错误码 → 中文原因（红条） */
export function errorReason(code: string | undefined, message?: string): string {
  const base: Record<string, string> = {
    rejected_hard: '硬拒：该语句不允许执行',
    readonly_connection: '只读连接：拒绝写入',
    driver_missing: '驱动缺失',
    sql_error: 'SQL 错误',
    not_found: '未找到 / 已过期',
    unauthorized: '未授权（401）',
    confirmation_required: '需二次确认：破坏性语句须在界面批准并确认',
    invalid_request: '请求不合法',
    approval_rejected: '审批已拒绝',
  }
  const head = (code && base[code]) || '执行失败'
  return message ? `${head} · ${message}` : head
}
