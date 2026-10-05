/**
 * 微调映射（v3，唯一真源）。
 *
 * 向导第③步与展厅「微调」面板**共用这一份**：品牌色 → `seedColor`；疏密 → `density`；
 * 圆润度 → `radiusBase`；动效 → `motionScale`；其余参数一律沿用预设 `request`。
 *
 * 为什么单列：这两处若各写一份映射，迟早出现"向导选的圆润度"和"展厅选的圆润度"对不上，
 * 而同一件事有两份真相正是本插件反复踩的坑（同源纪律）。
 */
import type { GenerateRequest } from '../api'

/** 疏密档（label 为大白话，与 §U 契约「疏密」控件选项一致） */
export type DensityId = 'comfortable' | 'default' | 'compact'

export interface DensityOption {
  id: DensityId
  label: string
}

export const DENSITY_OPTIONS: readonly DensityOption[] = [
  { id: 'comfortable', label: '舒展' },
  { id: 'default', label: '适中' },
  { id: 'compact', label: '紧凑' },
]

/** 动效档（克制度 → motionScale 数值） */
export const MOTION_OPTIONS: readonly { label: string; value: number }[] = [
  { label: '克制', value: 0.8 },
  { label: '适中', value: 1 },
  { label: '活泼', value: 1.2 },
]

export const RADIUS_MIN = 2
export const RADIUS_MAX = 16
export const RADIUS_DEFAULT = 8

/**
 * 风格轴取值（M3）。**键与值都来自后端 `meta.styleAxes`**（键 = 轴声明的 `field`，值 = 该轴的某个取值），
 * 本文件刻意不列任何轴名/取值的字面量清单 —— 抄一份就等于造出第二份真相，
 * 后端加第八条轴时界面会安静地少一条（`design/vocabulary.test.ts` 会把它抓红）。
 */
export type StyleAxesState = Record<string, string | number>

/** 微调状态（向导与展厅共用的形状） */
export interface TuneState {
  /** 品牌色（`#rgb`/`#rrggbb`）；空串 = 跟随预设种子色 */
  seedColor: string
  density: DensityId
  radiusBase: number
  motionScale: number
  /** 风格轴：缺键 = 跟随预设（预设也没带 = 后端默认值） */
  axes: StyleAxesState
}

/** 无预设时的默认微调（通用中性：适中疏密、8px 圆角、标准动效、轴全跟随） */
export function defaultTune(): TuneState {
  return { seedColor: '', density: 'default', radiusBase: RADIUS_DEFAULT, motionScale: 1, axes: {} }
}

/** `meta.styleAxes` → 控件要写的字段名清单（顺序即界面排布顺序，前端不重排） */
export function axisFields(axes: { field: string }[] | undefined): string[] {
  return (axes ?? []).map((a) => a.field)
}

/** 从预设请求里挑出"它本来就带着的轴取值"，只认 meta 声明过的字段 */
export function axesFromPreset(presetRequest: GenerateRequest | null, fields: string[]): StyleAxesState {
  const src = (presetRequest ?? {}) as Record<string, unknown>
  const out: StyleAxesState = {}
  for (const field of fields) {
    const v = src[field]
    if (typeof v === 'string' && v !== '') out[field] = v
    else if (typeof v === 'number') out[field] = v
  }
  return out
}

/**
 * 从预设 `request` 出发布微调初值：滑块/选项一开始就显示"这套预设的真实值"，
 * 而不是界面自己拍一个默认（否则用户不动滑块也看到 8px，与画布上的 4px 对不上）。
 */
export function tuneFromPreset(presetRequest: GenerateRequest | null, fields: string[] = []): TuneState {
  const r = presetRequest ?? {}
  return {
    seedColor: typeof r.seedColor === 'string' ? r.seedColor : '',
    density: isDensity(r.density) ? r.density : 'default',
    radiusBase: typeof r.radiusBase === 'number' ? r.radiusBase : RADIUS_DEFAULT,
    motionScale: typeof r.motionScale === 'number' ? r.motionScale : 1,
    axes: axesFromPreset(presetRequest, fields),
  }
}

function isDensity(v: unknown): v is DensityId {
  return v === 'comfortable' || v === 'default' || v === 'compact'
}

/** 品牌色合法性：空串（跟随预设）或 `#rgb` / `#rrggbb` */
export function isValidBrandColor(v: string): boolean {
  const s = v.trim()
  return s === '' || /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(s)
}

/**
 * 把微调叠加到预设 `request` 上（后者覆盖前者）：
 * 品牌色为空则**保留**预设种子色（`delete` 掉本次写入的空值），疏密/圆润度/动效恒以微调为准。
 * 由于 `tuneFromPreset` 让初值等于预设值，用户不动任何控件时结果与预设逐字一致。
 */
export function tuneToRequest(presetRequest: GenerateRequest | null, tune: TuneState): GenerateRequest {
  const out: GenerateRequest = { ...(presetRequest ?? {}) }
  const seed = tune.seedColor.trim()
  if (seed) out.seedColor = seed
  else if (!out.seedColor) delete out.seedColor // 预设也没值时不留空串键（有值则保留预设种子色）
  out.density = tune.density
  out.radiusBase = tune.radiusBase
  out.motionScale = tune.motionScale
  // 风格轴逐条写回：控件初值等于预设值，所以"用户一个没动"时结果与预设逐字一致；
  // 空值一律不写（让后端按 null=默认 处理），避免把空串当成一个取值
  const target = out as Record<string, unknown>
  for (const [field, value] of Object.entries(tune.axes ?? {})) {
    if (value === '' || value === null || value === undefined) continue
    target[field] = value
  }
  return out
}