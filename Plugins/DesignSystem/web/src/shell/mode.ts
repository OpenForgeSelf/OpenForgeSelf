/**
 * 四模式外壳的模式纯函数（v3）。
 *
 * 模式 = 开始（向导）/ 展厅（试穿）/ 工作台（14 入口）/ 交付与接入。
 * 本文件不含 DOM：`resolveInitialMode` 的判据（哈希 / 有无项目 / 存储值）由调用方传入，
 * 便于 vitest 直接断言 AC6 的默认模式规则。
 */
export type Mode = 'start' | 'showroom' | 'workbench' | 'delivery'

export const MODES: readonly Mode[] = ['start', 'showroom', 'workbench', 'delivery']

/** 展示名（§U DOM 契约：模式条可访问名恰为这四个） */
export const MODE_LABELS: Record<Mode, string> = {
  start: '开始',
  showroom: '展厅',
  workbench: '工作台',
  delivery: '交付与接入',
}

export function isMode(v: unknown): v is Mode {
  return typeof v === 'string' && (MODES as readonly string[]).includes(v)
}

/** 模式持久化键（FR2：切换写回 `ds.mode`） */
export const STORAGE_KEY = 'ds.mode'

/** 从 `location.hash`（形如 `#/workbench/projects`）提取模式；非法一律 null */
export function modeFromHash(hash: string): Mode | null {
  const m = /^#\/([a-z-]+)/.exec((hash ?? '').trim())
  if (!m) return null
  return isMode(m[1]) ? m[1] : null
}

/** 读存储的模式；localStorage 不可用（隐私模式 / 被禁）时静默回 null */
export function readStoredMode(): Mode | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw && isMode(raw) ? raw : null
  } catch {
    return null
  }
}

/** 写存储的模式；写失败静默（切换仍生效，只是刷新后不还原） */
export function storeMode(mode: Mode): void {
  try {
    localStorage.setItem(STORAGE_KEY, mode)
  } catch {
    /* 静默 */
  }
}

export interface InitialModeInput {
  /** 当前 URL 哈希（原样传入，内部解析） */
  hash: string
  /** 是否存在"非归档"项目（决定无存储值时默认进开始还是展厅） */
  hasProjects: boolean
  /** 已读出的存储模式（`readStoredMode()` 的产物；null = 无/非法） */
  stored: Mode | null
}

/**
 * FR2 默认模式规则（优先级从高到低）：
 * ① URL 哈希合法 → 用它；② 无非归档项目 → `start`；③ `ds.mode` 合法 → 用它；④ → `showroom`。
 */
export function resolveInitialMode(input: InitialModeInput): Mode {
  const fromHash = modeFromHash(input.hash)
  if (fromHash) return fromHash
  if (!input.hasProjects) return 'start'
  return input.stored ?? 'showroom'
}
