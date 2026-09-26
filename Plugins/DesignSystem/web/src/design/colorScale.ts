/**
 * 色阶推导（通用）。
 *
 * 设计原则（写在这里以便后来者理解为何这样做）：
 * - **品牌色/辅助色**随品牌色相派生：给定主色相 H，按固定明度-饱和度曲线生成 50–900。
 *   曲线经 Stardust（紫 H≈262）反推校准，保证任意色相都能得到观感一致的色阶。
 * - **中性色是稳定导轨，不随品牌色相漂移**：所有设计系统共用一条冷灰 slate 轨道，
 *   这样换品牌时不会出现「中性色也跟着变绿/变紫」的脏观感。
 * - **语义色固定**：success/warning/danger/info 承载状态语义，必须跨品牌一致，否则用户要重新学习。
 */

import type { Scale } from './schema'

/** HSL → HEX（h: 0–360, s/l: 0–100）。 */
export function hslToHex(h: number, s: number, l: number): string {
  const sN = s / 100
  const lN = l / 100
  const k = (n: number) => (n + (h % 360) / 30) % 12
  const a = sN * Math.min(lN, 1 - lN)
  const f = (n: number) => lN - a * Math.max(-1, Math.min(k(n) - 3, Math.min(9 - k(n), 1)))
  const toHex = (x: number) => {
    const v = Math.round(255 * x)
    return v.toString(16).padStart(2, '0')
  }
  return `#${toHex(f(0))}${toHex(f(8))}${toHex(f(4))}`
}

/** 品牌色阶的「明度-饱和度」曲线（由 Stardust 紫反推校准）。 */
const BRAND_CURVE: { step: string; l: number; s: number }[] = [
  { step: '50', l: 97, s: 96 },
  { step: '100', l: 96, s: 94 },
  { step: '200', l: 92, s: 92 },
  { step: '300', l: 85, s: 90 },
  { step: '400', l: 75, s: 90 },
  { step: '500', l: 66, s: 88 },
  { step: '600', l: 58, s: 84 },
  { step: '700', l: 51, s: 75 },
  { step: '800', l: 42, s: 71 },
  { step: '900', l: 34, s: 66 },
]

/** 由品牌色相生成 50–900 色阶。 */
export function brandScale(hue: number): Scale {
  const out: Scale = {}
  for (const c of BRAND_CURVE) {
    out[c.step] = hslToHex(hue, c.s, c.l)
  }
  return out
}

/** 中性导轨（冷灰 slate，跨品牌共用，不随色相漂移）。 */
export const NEUTRAL: Scale = {
  '50': '#f8fafc',
  '100': '#f1f5f9',
  '200': '#e2e8f0',
  '300': '#cbd5e1',
  '400': '#94a3b8',
  '500': '#64748b',
  '600': '#475569',
  '700': '#334155',
  '800': '#1e293b',
  '900': '#0f172a',
  '950': '#020617',
}

/** 语义色（跨品牌固定）。 */
export const SEMANTIC: Record<string, string> = {
  success: '#22c55e',
  'success-soft': '#dcfce7',
  warning: '#f59e0b',
  'warning-soft': '#fef3c7',
  danger: '#ef4444',
  'danger-soft': '#fee2e2',
  info: '#3b82f6',
  'info-soft': '#dbeafe',
}

/** 表面层级：背景带极淡品牌染色，其余走中性导轨。 */
export function surfaceSet(hue: number): Record<string, string> {
  return {
    'surface-bg': hslToHex(hue, 34, 95),
    'surface-1': '#ffffff',
    'surface-2': NEUTRAL['50'],
    'surface-3': NEUTRAL['100'],
  }
}

/** 前景层级：fg 走中性导轨，link 走辅助色（不用主色，避免「链接色=品牌色」的信息混淆）。 */
export function textSet(accent600: string): Record<string, string> {
  return {
    'fg-1': NEUTRAL['900'],
    'fg-2': NEUTRAL['700'],
    'fg-3': NEUTRAL['500'],
    'fg-4': NEUTRAL['400'],
    link: accent600,
  }
}

/** 辅助色相：与主色形成固定的三角关系（Stardust 紫 262 → 青 187 即由此推导）。 */
export function accentHueOf(hue: number): number {
  return (hue + 282) % 360
}
