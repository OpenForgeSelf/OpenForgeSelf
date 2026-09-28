import type { FolderScanView, FolderSizeRow } from '../types/fileTools'

/**
 * 目录排行面板的纯编排层（无 Vue / 无 element-plus 依赖，便于 vitest 直接锁死语义）。
 * 这里刻意不做字节格式化 —— 展示串由后端 `*Formatted` 给，避免同一数值两套口径。
 */

export type FolderEmptyState =
  | 'idle'
  | 'running'
  | 'emptyDir'
  | 'noPermission'
  | 'failed'
  | 'expired'

/** 空态分级：不同成因给不同文案，禁止一律「暂无数据」 */
export const EMPTY_STATE_TEXT: Record<FolderEmptyState, string> = {
  idle: '输入目录后点「扫描」，这里会列出各子目录的占用排行',
  running: '扫描进行中，已扫到的部分结果会先显示出来',
  emptyDir: '该目录下没有文件与子目录（空目录）',
  noPermission: '该目录下的条目没有读取权限，未能统计到任何字节',
  failed: '扫描失败，原因见下方错误详情',
  expired: '任务已不在内存中（宿主重启或任务表已淘汰），请重新扫描；历史结果请看快照'
}

export function pickEmptyState(view: FolderScanView | null | undefined): FolderEmptyState {
  if (!view) return 'expired'
  if (view.state === 0 || view.state === 1) return 'running' // Queued/Running
  if (view.state === 3) return 'failed' // Failed
  if (view.rootTotalBytes > 0 || view.items.length > 0) return 'idle'
  if (view.inaccessibleCount > 0) return 'noPermission'
  return 'emptyDir'
}

/** 排行区实际要展示的行：普通行 + 「其他」+「本级文件」（后者排最后，便于把占比凑满 100%） */
export function displayRows(
  holder: {
    items: FolderSizeRow[]
    otherRow: FolderSizeRow | null
    rootOwnRow: FolderSizeRow | null
  } | null | undefined
): FolderSizeRow[] {
  if (!holder) return []
  const rows = [...holder.items]
  if (holder.otherRow) rows.push(holder.otherRow)
  if (holder.rootOwnRow) rows.push(holder.rootOwnRow)
  return rows
}

/**
 * 分区守恒余量：根总量 − Σ排行行 − 其他 − 根本级。
 * 排行恒为「直接子目录」这一层切片，三者互不重叠，理论上恒等于 0；
 * 非 0 说明后端算错或结果不完整，界面据此显式告警，而不是让用户以为占比凑满了。
 */
export function partitionRemainder(view: FolderScanView | null | undefined): number {
  if (!view) return 0
  const listed = view.items.reduce((sum, row) => sum + row.totalBytes, 0)
  const other = view.otherRow?.totalBytes ?? 0
  return view.rootTotalBytes - listed - other - view.rootOwnBytes
}

export function partitionOk(view: FolderScanView | null | undefined): boolean {
  if (!view) return true
  const tolerance = Math.max(1, Math.round(view.rootTotalBytes * 0.005))
  return Math.abs(partitionRemainder(view)) <= tolerance
}

/**
 * 二次确认的可单测形状：确认动作由调用方注入（面板里传 `ElMessageBox.confirm` 的包装），
 * 这样「用户点取消 ⇒ 一个请求都不发」这条关键语义能被单测直接锁死，且不依赖弹窗组件。
 * @returns 是否真正执行了动作
 */
export async function runAfterConfirm(
  ask: () => Promise<boolean>,
  action: () => Promise<void>
): Promise<boolean> {
  if (!(await ask())) return false
  await action()
  return true
}

/**
 * 轮询防闪：进度与结果未变时返回 false，调用方据此**跳过赋值**，避免整块重渲染闪动。
 * 只比对会影响展示的字段，不比 timestamp/duration（那两个每次都在变，比了就等于没防）。
 */
export function shouldApplyProgress(
  previous: FolderScanView | null | undefined,
  next: FolderScanView
): boolean {
  if (!previous) return true
  if (previous.scanId !== next.scanId) return true
  if (previous.state !== next.state) return true
  if (previous.rootTotalBytes !== next.rootTotalBytes) return true
  if (previous.rootOwnBytes !== next.rootOwnBytes) return true
  if (previous.fileCount !== next.fileCount) return true
  if (previous.directoryCount !== next.directoryCount) return true
  if (previous.inaccessibleCount !== next.inaccessibleCount) return true
  if (previous.skippedReparseCount !== next.skippedReparseCount) return true
  if (previous.truncated !== next.truncated) return true
  if (previous.error !== next.error) return true
  if (previous.items.length !== next.items.length) return true

  for (let i = 0; i < next.items.length; i++) {
    const a = previous.items[i]
    const b = next.items[i]
    if (!a || a.relativePath !== b.relativePath || a.totalBytes !== b.totalBytes) return true
  }
  return false
}
