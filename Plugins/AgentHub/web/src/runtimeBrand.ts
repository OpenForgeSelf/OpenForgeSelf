/**
 * Per-runtime brand icons from `@botiverse/oar` assets (copied under src/assets/brands).
 * Inline as data URLs so vite lib mode does not rename them to style.svg.
 * Missing icon → brand-name glyph only (caller renders letter).
 */

import grokSvg from './assets/brands/grok.svg?raw'
import claudeSvg from './assets/brands/claude.svg?raw'
import codexSvg from './assets/brands/codex.svg?raw'
import opencodeSvg from './assets/brands/opencode.svg?raw'
import piSvg from './assets/brands/pi.svg?raw'
import kimiSvg from './assets/brands/kimi.svg?raw'
import qwenSvg from './assets/brands/qwen.svg?raw'
import qoderSvg from './assets/brands/qoder.svg?raw'
import codebuddySvg from './assets/brands/codebuddy.svg?raw'
import cursorSvg from './assets/brands/cursor.svg?raw'
import dshSvg from './assets/brands/dsh.svg?raw'
import factorySvg from './assets/brands/factory.svg?raw'
import antigravitySvg from './assets/brands/antigravity.svg?raw'
import devinSvg from './assets/brands/devin.svg?raw'
import morphSvg from './assets/brands/morph.svg?raw'

const RAW: Record<string, string> = {
  grok: grokSvg,
  claude: claudeSvg,
  codex: codexSvg,
  opencode: opencodeSvg,
  pi: piSvg,
  kimi: kimiSvg,
  qwen: qwenSvg,
  qoder: qoderSvg,
  codebuddy: codebuddySvg,
  cursor: cursorSvg,
  dsh: dshSvg,
  factory: factorySvg,
  antigravity: antigravitySvg,
  devin: devinSvg,
  morph: morphSvg,
}

const dataUrlCache = new Map<string, string>()

function toDataUrl(svg: string): string {
  // <img> does not inherit CSS color — replace currentColor so brand marks stay visible.
  const normalized = svg
    .trim()
    .replace(/currentColor/gi, '#1f1a17')
  const encoded = encodeURIComponent(normalized)
    .replace(/'/g, '%27')
    .replace(/"/g, '%22')
  return `data:image/svg+xml,${encoded}`
}

/** Normalize vendor / runtime id keys (case-insensitive, strip spaces). */
export function normalizeBrandKey(id?: string | null): string {
  return (id ?? '').trim().toLowerCase()
}

/**
 * Resolve brand icon data URL for a runtimeId or vendor.
 * Returns null when no asset — UI should fall back to a glyph.
 */
export function brandIconUrl(runtimeOrVendor?: string | null): string | null {
  const key = normalizeBrandKey(runtimeOrVendor)
  if (!key || key === 'scripted') return null
  const raw = RAW[key]
  if (!raw) return null
  let url = dataUrlCache.get(key)
  if (!url) {
    url = toDataUrl(raw)
    dataUrlCache.set(key, url)
  }
  return url
}

/** Single uppercase glyph from brand / runtime id (fallback when icon missing). */
export function brandGlyph(runtimeOrVendor?: string | null, displayName?: string | null): string {
  const src = (displayName || runtimeOrVendor || '?').trim()
  if (!src) return '?'
  const ch = Array.from(src)[0] ?? '?'
  return ch.toUpperCase()
}
