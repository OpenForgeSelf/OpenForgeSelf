import { runStatusName, type AgentRunStatus } from './types'

export function runStatusText(code: number): string {
  switch (runStatusName(code)) {
    case 'pending':
      return '等待'
    case 'planning':
      return '规划中'
    case 'running':
      return '运行中'
    case 'completed':
      return '完成'
    case 'stuck':
      return '卡住'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return runStatusName(code)
  }
}

export function runStatusTextByName(status: AgentRunStatus): string {
  switch (status) {
    case 'pending':
      return '等待'
    case 'planning':
      return '规划中'
    case 'running':
      return '运行中'
    case 'completed':
      return '完成'
    case 'stuck':
      return '卡住'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return status
  }
}

/** 需要在 composer 上方 Run 条展示的 Run 状态。 */
export function isActiveRunStatus(code: number): boolean {
  const n = runStatusName(code)
  return n === 'pending' || n === 'planning' || n === 'running' || n === 'stuck'
}

export function isActiveRunStatusName(status: AgentRunStatus): boolean {
  return status === 'pending' || status === 'planning' || status === 'running' || status === 'stuck'
}
