/**
 * 设计产物的本地持久化（localStorage）。
 *
 * 定位：在**不引入后端变更**的前提下，让设计产物跨刷新存活、可回溯、可复用。
 * 后端持久化（跨设备/团队共享）属高风险变更（需实体 + 迁移 + 鉴权），
 * 按 AGENTS §6.4 需升级给人确认后再做，故此处只做本地层（见 README §7.3 G2）。
 *
 * 存储内容：
 * - 当前工作区：需求描述 + 原型选择 + 最近一次产出的设计（刷新即恢复）
 * - 历史记录：最近 N 条（默认 10），可加载 / 删除 / 清空
 */

import type { DesignSystem } from './schema'

const KEY_WORKSPACE = 'forgeself.design-system.workspace.v1'
const KEY_HISTORY = 'forgeself.design-system.history.v1'

/** 历史条目上限（超出丢弃最旧）。 */
export const HISTORY_LIMIT = 10

/** 当前工作区快照。 */
export interface WorkspaceState {
  brief: string
  industry: string
  design: DesignSystem | null
  updatedAt: string
}

/** 历史条目。 */
export interface HistoryEntry {
  id: string
  title: string
  brief: string
  industry: string
  savedAt: string
  design: DesignSystem
}

function safeGet(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    // 隐私模式 / 禁用存储：降级为不持久化，不影响主流程
    return null
  }
}

function safeSet(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    /* 配额超限或禁用存储：静默降级 */
  }
}

/** 读取工作区快照（无或损坏时返回 null）。 */
export function loadWorkspace(): WorkspaceState | null {
  const raw = safeGet(KEY_WORKSPACE)
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as WorkspaceState
    if (typeof parsed?.brief !== 'string') return null
    return parsed
  } catch {
    return null
  }
}

/** 保存工作区快照。 */
export function saveWorkspace(state: WorkspaceState): void {
  safeSet(KEY_WORKSPACE, JSON.stringify(state))
}

/** 读取历史记录（新→旧）。 */
export function loadHistory(): HistoryEntry[] {
  const raw = safeGet(KEY_HISTORY)
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw) as HistoryEntry[]
    return Array.isArray(parsed)
      ? parsed.filter(
          (e) => e && typeof e.id === 'string' && (e.design as { meta?: { seed?: unknown } })?.meta?.seed,
        )
      : []
  } catch {
    return []
  }
}

function writeHistory(list: HistoryEntry[]): void {
  safeSet(KEY_HISTORY, JSON.stringify(list.slice(0, HISTORY_LIMIT)))
}

/**
 * 把一次生成结果写入历史（同 brief 视为同一条目，更新而非重复追加）。
 * @returns 写入后的历史列表
 */
export function pushHistory(design: DesignSystem): HistoryEntry[] {
  const list = loadHistory()
  const entry: HistoryEntry = {
    id: `${design.meta.seed.industry}-${hash(design.meta.brief)}`,
    title: design.meta.name,
    brief: design.meta.brief,
    industry: design.meta.seed.industry,
    savedAt: design.meta.generatedAt,
    design,
  }
  const rest = list.filter((e) => e.id !== entry.id)
  const next = [entry, ...rest].slice(0, HISTORY_LIMIT)
  writeHistory(next)
  return next
}

/** 删除一条历史。 */
export function removeHistory(id: string): HistoryEntry[] {
  const next = loadHistory().filter((e) => e.id !== id)
  writeHistory(next)
  return next
}

/** 清空历史。 */
export function clearHistory(): HistoryEntry[] {
  writeHistory([])
  return []
}

/** 简易字符串哈希（用于历史条目 id，避免 key 过长与特殊字符）。 */
function hash(s: string): string {
  let h = 0
  for (let i = 0; i < s.length; i++) {
    h = (h << 5) - h + s.charCodeAt(i)
    h |= 0
  }
  return (h >>> 0).toString(36)
}
