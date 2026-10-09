/**
 * 界面用的纯函数集合（无副作用、可单测）。
 *
 * 为什么单独成文件：这些判断（"能不能下发""缺哪几栏""默认勾哪些工件""标签什么颜色"）
 * 一旦写进 .vue 模板里就很难测；集中成纯函数后 vitest 能直接锁死，
 * 而它们恰恰是"界面看着有数字、其实数字是假的"的高发区。
 */
import type { ArtifactFile, ArtifactSet, TodoItem, TodoStage } from './types'
import { RequiredFieldLabels, StageLabels } from './types'

/** 阶段标签配色（Element Plus tag type）。终态与进行中要一眼分得开。 */
export function stageTagType(stage: TodoStage | string): 'info' | 'primary' | 'success' | 'warning' | 'danger' {
  switch (stage) {
    case 'Draft': return 'info'
    case 'Ready': return 'primary'
    case 'Dispatched': return 'primary'
    case 'Running': return 'warning'
    case 'Blocked': return 'danger'
    case 'Review': return 'warning'
    case 'Done': return 'success'
    case 'Cancelled': return 'info'
    default: return 'info'
  }
}

export function stageLabel(stage: TodoStage | string): string {
  return StageLabels[stage as TodoStage] ?? String(stage)
}

/** 优先级徽标文本（与 AGENTS.md §0.2 的 P1>P2>P3 口径一致）。 */
export function priorityLabel(priority: number): string {
  return priority === 1 ? 'P1' : priority === 2 ? 'P2' : priority === 3 ? 'P3' : 'P?'
}

/** 能否下发：只看服务端回传的 missing（状态机的唯一真源在后端，界面不重算）。 */
export function missingLabels(missing: string[] | undefined): string {
  if (!missing || missing.length === 0) return ''
  return missing.map(k => RequiredFieldLabels[k] ?? k).join('、')
}

/** 未关联项目的任务排在后面（同一项目内再按 P1>P2>P3、创建时间新→旧）。 */
export function sortTasks(items: TodoItem[]): TodoItem[] {
  return [...items].sort((a, b) => {
    const byProject = Number(!a.projectId) - Number(!b.projectId)
    if (byProject !== 0) return byProject
    const byPriority = (a.priority || 3) - (b.priority || 3)
    if (byPriority !== 0) return byPriority
    return (b.createdAt || '').localeCompare(a.createdAt || '')
  })
}

/** 工件导入的默认勾选：后端标了 isCore（01/02/03/04），界面照抄，不自己数序号。 */
export function defaultFileSelection(files: ArtifactFile[]): string[] {
  return files.filter(f => f.isCore).map(f => f.name)
}

/** 选中的目录里默认可勾的文件集合。 */
export function defaultSelectionFor(set: ArtifactSet | null): string[] {
  return set ? defaultFileSelection(set.files) : []
}

export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/** 相对时间：1 小时内按分钟，1 天内按小时，更久按天（列表右侧那一小栏放不下完整时间戳）。 */
export function relativeTime(iso: string | null | undefined, now: Date = new Date()): string {
  if (!iso) return '—'
  const then = new Date(iso)
  if (Number.isNaN(then.getTime())) return String(iso)
  const minutes = Math.floor((now.getTime() - then.getTime()) / 60_000)
  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes} 分钟前`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} 小时前`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days} 天前`
  return then.toLocaleDateString()
}

export function shortPath(path: string, max = 46): string {
  const text = (path ?? '').trim()
  if (text.length <= max) return text
  return `…${text.slice(-(max - 1))}`
}

/** 耗时读数：毫秒级原样、秒级保留一位小数，避免"0.003s"这种看不清的东西。 */
export function formatElapsed(ms: number): string {
  if (!ms || ms <= 0) return '—'
  if (ms < 1000) return `${ms} ms`
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`
  return `${Math.floor(ms / 60_000)} 分 ${Math.round((ms % 60_000) / 1000)} 秒`
}

/**
 * 复制提示词。navigator.clipboard 在非安全上下文/无权限时不可用，
 * 退到 textarea + execCommand；两条路都失败时返回 false，让调用方如实提示"请手工全选复制"，
 * 绝不静默返回"成功"。
 */
export async function copyText(text: string): Promise<boolean> {
  if (!text) return false
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // 落到兜底分支
  }
  try {
    const ta = document.createElement('textarea')
    ta.value = text
    ta.setAttribute('readonly', '')
    ta.style.position = 'fixed'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    const ok = document.execCommand('copy')
    document.body.removeChild(ta)
    return ok
  } catch {
    return false
  }
}

/** 详情页"未保存改动"判定：只比对会被表单改到的字段。 */
export function formSnapshot(task: TodoItem): string {
  return JSON.stringify([
    task.title, task.remark ?? '', task.objective, task.content, task.acceptance,
    task.verification, task.allowedScope, task.forbiddenScope, task.priority, task.assignee
  ])
}

/** 阶段可达性：完全照服务端给的 allowedTargets（界面不抄一份流转表，防"按钮能点、后端 409"）。 */
export function allowedTargetsOf(task: TodoItem | null): string[] {
  return task?.allowedTargets ?? []
}

export type TagType = 'info' | 'primary' | 'success' | 'warning' | 'danger'

/**
 * 委派实时状态 → 徽标元数据（列表实时徽标，FR-3.1）。
 * 词表以 AgentHub 为准（Queued|Running|AwaitingPermission|Succeeded|Failed|Cancelled|Timeout|Interrupted）；
 * 未知值原样展示不编造。空状态（未拿到批量数据）交给调用方走 agentFallbackByStage。
 */
export function agentStatusMeta(status: string | undefined | null): { label: string; type: TagType } {
  switch (status) {
    case 'Queued': return { label: '排队中', type: 'info' }
    case 'Running': return { label: '执行中', type: 'warning' }
    case 'AwaitingPermission': return { label: '待授权', type: 'warning' }
    case 'Succeeded': return { label: '已成功', type: 'success' }
    case 'Failed': return { label: '失败', type: 'danger' }
    case 'Cancelled': return { label: '已取消', type: 'info' }
    case 'Timeout': return { label: '已超时', type: 'danger' }
    case 'Interrupted': return { label: '已中断', type: 'danger' }
    default: return { label: status || '执行中', type: 'warning' }
  }
}

/**
 * 批量状态数据缺失时的阶段兜底徽标（FR-3.1）：stage 是任务生命周期（流转有延迟），
 * 徽标标题注明「按阶段」；agent 实时状态以详情页 agent-status 为准。
 */
export function agentFallbackByStage(stage: TodoStage | string): { label: string; type: TagType } {
  switch (stage) {
    case 'Dispatched':
    case 'Running': return { label: '执行中', type: 'warning' }
    case 'Review': return { label: '待验收', type: 'warning' }
    case 'Done': return { label: '已完成', type: 'success' }
    case 'Failed':
    case 'Cancelled':
    case 'Blocked': return { label: '已结束', type: 'info' }
    default: return { label: '已下发', type: 'primary' }
  }
}

/** agent 名显示兜底：未知（null/空）时显示 agent#id，不编名字。 */
export function agentNameOrFallback(name: string | null | undefined, id: number | undefined): string {
  if (name) return name
  return id ? `agent#${id}` : 'agent'
}
