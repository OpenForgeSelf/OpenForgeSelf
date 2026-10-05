/**
 * 展厅视图档（2026-10-04 输入22：「应该缩放，或者可以最大化，或者可以自由调尺寸，并且有滚动条，不能只看到部分」）。
 *
 * 这里只放**纯函数**（缩放比与持久化值解析），因为这两件事必须能被单测钉住；
 * 稿宽的唯一真源仍是 `scenes.ts` 的 `DEVICES`，本模块不写第二份数字。
 */

export type ViewMode = 'fit' | 'actual' | 'max'

export const VIEW_MODES: readonly { id: ViewMode; label: string; hint: string }[] = [
  { id: 'fit', label: '适应', hint: '按画布可用宽度等比缩小，右侧不再被裁' },
  { id: 'actual', label: '1:1', hint: '真实像素显示，超出部分用纵横滚动条到达' },
  { id: 'max', label: '最大化', hint: '让开两侧栏，画布吃满可用宽高' },
]

/**
 * 适应档的缩放比：`k = min(1, 可用宽 / 稿宽)`。
 *
 * 上限 1 是刻意的：放大到 1 以上会让 1280 的桌面稿比它真实的设计尺寸还大，
 * 看到的字距与留白都是失真的，宁可用 1:1 + 滚动条也不放大。
 * 可用宽拿不到（0 / 负 / NaN，例如元素还没挂载或父级 `display:none`）时返回 1，
 * 交给 1:1 的滚动条兜底——返回 0 会把画布整个压没，那是更坏的失败。
 */
export function fitScale(availWidth: number, frameWidth: number): number {
  if (!Number.isFinite(availWidth) || !Number.isFinite(frameWidth)) return 1
  if (availWidth <= 0 || frameWidth <= 0) return 1
  return Math.min(1, availWidth / frameWidth)
}

/** localStorage 里可能躺着任何值（手改、旧版本残留）⇒ 只认三个已知档，其余回落 `fit`。 */
export function parseViewMode(raw: unknown): ViewMode {
  return raw === 'actual' || raw === 'max' || raw === 'fit' ? raw : 'fit'
}

/** 视图档持久化键（与 `ds.mode` / `ds.pro-terms` 同一命名族） */
export const VIEW_STORAGE_KEY = 'ds.showroom.view'

/** 读存储的视图档；localStorage 不可用（隐私模式 / 被禁）时静默回默认档 */
export function readStoredView(): ViewMode {
  try {
    return parseViewMode(localStorage.getItem(VIEW_STORAGE_KEY))
  } catch {
    return 'fit'
  }
}

/** 写存储的视图档；写失败静默（切换仍生效，只是刷新后不还原） */
export function storeView(mode: ViewMode): void {
  try {
    localStorage.setItem(VIEW_STORAGE_KEY, mode)
  } catch {
    /* 静默 */
  }
}
