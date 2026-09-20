/**
 * 首页「常用功能」入口派生与排序 — 纯函数层（不依赖 Vue / 存储），便于单测。
 *
 * 数据源单一：由调用方传入 `features.ts` 内置列表与后端插件清单贡献合并后的
 * FeatureItem[]（合并逻辑见 features.ts 的 mergeFeatureList），本模块只负责：
 *   ① 把「有可点击路由」的功能派生为首页入口
 *   ② 按「固定(pin) → 热度(带时间衰减) → 原始顺序」排序并截断数量
 *
 * 为什么 key 用 path 而不是 feature id：全局使用统计（useOpenPage）在导航层
 * 只能拿到路由 path，用 path 做键才能与统计口径对齐；插件动态注册的功能
 * 亦然（它们只有 route，没有内置 id）。
 */

import type { FeatureItem } from './features'

/** 首页入口：一条可点击的功能/插件入口。 */
export interface HomeEntry {
  /** 稳定标识 = 路由 path */
  key: string
  label: string
  /** features.ts 的图标名 */
  icon: string
  path: string
}

/** 使用度快照：调用方（store）提供，本模块只读。 */
export interface UsageSnapshot {
  /** path → 每次访问的时间戳（ms）列表 */
  visits: Record<string, number[]>
  /** 被固定的 path（按固定先后从前到后） */
  pinned: string[]
}

/** 首页默认展示的入口数量（其余走「查看全部」）。 */
export const HOME_ENTRY_LIMIT = 12

/** 热度半衰期（天）：超过 7 天的一次访问权重减半，避免陈旧热点永久霸榜。 */
export const USAGE_HALF_LIFE_DAYS = 7

/** 单个入口最多保留的访问时间戳条数，防止 localStorage 无限膨胀。 */
export const MAX_VISITS_PER_KEY = 50

const HALF_LIFE_MS = USAGE_HALF_LIFE_DAYS * 86_400_000
/** 超过 8 个半衰期（≈56 天）贡献已趋近 0，直接跳过计算。 */
const MAX_EFFECTIVE_MS = HALF_LIFE_MS * 8

/**
 * 把功能列表派生为首页入口。
 * 过滤规则：必须启用 + 必须有可跳转路径（如 `scheduler` 这类无页面功能不上首页）。
 */
export function buildHomeEntries(features: FeatureItem[]): HomeEntry[] {
  const result: HomeEntry[] = []
  for (const f of features) {
    if (!f.enabled || !f.path) continue
    result.push({ key: f.path, label: f.name, icon: f.icon, path: f.path })
  }
  return result
}

/**
 * 计算入口热度分：对每次访问按指数衰减累加。
 * score = Σ 2^(-Δt / 半衰期)，刚访问过的 1 次 ≈ 1 分，7 天前的一次 ≈ 0.5 分。
 * 未来时间戳（时钟回拨）与超过 8 个半衰期的记录不计入。
 */
export function usageScore(visits: number[] | undefined, now: number): number {
  if (!visits || visits.length === 0) return 0
  let score = 0
  for (const t of visits) {
    const dt = now - t
    if (dt < 0 || dt > MAX_EFFECTIVE_MS) continue
    score += Math.pow(2, -dt / HALF_LIFE_MS)
  }
  return score
}

/** 排序后的入口，附带排序依据，便于 UI 标记「固定 / 高频」。 */
export interface RankedEntry {
  entry: HomeEntry
  score: number
  pinned: boolean
}

/**
 * 排序入口（不截断）：
 *   1. 固定的排最前（多个固定项之间再按热度排）
 *   2. 其次按热度分降序
 *   3. 分数相同（含冷启动全 0）时保持 features.ts 原始顺序 —— 保证冷启动不抖动
 */
export function rankEntriesDetailed(
  entries: HomeEntry[],
  usage: UsageSnapshot,
  now: number,
): RankedEntry[] {
  const pinnedSet = new Set(usage.pinned ?? [])
  const scored: Array<RankedEntry & { index: number }> = entries.map((entry, index) => ({
    entry,
    index,
    score: usageScore(usage.visits?.[entry.key], now),
    pinned: pinnedSet.has(entry.key),
  }))

  scored.sort((a, b) => {
    if (a.pinned !== b.pinned) return a.pinned ? -1 : 1
    if (b.score !== a.score) return b.score - a.score
    return a.index - b.index
  })

  return scored.map(({ entry, score, pinned }) => ({ entry, score, pinned }))
}

/**
 * 排序并取前 limit 条。冷启动（无使用数据）时等价于按 features.ts 原顺序取前 N 条。
 */
export function rankEntries(
  entries: HomeEntry[],
  usage: UsageSnapshot,
  now: number,
  limit: number = HOME_ENTRY_LIMIT,
): HomeEntry[] {
  return rankEntriesDetailed(entries, usage, now)
    .slice(0, limit)
    .map((r) => r.entry)
}
