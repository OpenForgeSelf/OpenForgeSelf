/**
 * URL 密码深链策略与「我已知晓风险」持久化。
 * 真源：docs/02-交互说明.md、docs/05-已知风险-密码入URL.md
 */
export type UrlPasswordMode = 'always-ack' | 'intranet' | 'disabled'

export const URL_PASSWORD_MODE_KEY = 'forge.self.smart-terminal.url-password-mode.v1'
export const URL_PASSWORD_ACK_KEY = 'forge.self.smart-terminal.url-password-risk-ack.v1'

const VALID: UrlPasswordMode[] = ['always-ack', 'intranet', 'disabled']

function storage(): Storage | null {
  try {
    if (typeof localStorage === 'undefined') return null
    return localStorage
  } catch {
    return null
  }
}

export function parseUrlPasswordMode(raw: string | null | undefined): UrlPasswordMode | null {
  if (!raw) return null
  const v = raw.trim().toLowerCase()
  return (VALID as string[]).includes(v) ? (v as UrlPasswordMode) : null
}

/** 解析当次覆盖（query）+ 持久设置；默认 always-ack（公网/演示更安全）。 */
export function resolveUrlPasswordMode(options?: {
  queryMode?: string | null
  storedMode?: string | null
}): UrlPasswordMode {
  const fromQuery = parseUrlPasswordMode(options?.queryMode)
  if (fromQuery) return fromQuery
  const store = storage()
  const stored = parseUrlPasswordMode(options?.storedMode ?? store?.getItem(URL_PASSWORD_MODE_KEY))
  return stored ?? 'always-ack'
}

export function getStoredAck(): string | null {
  return storage()?.getItem(URL_PASSWORD_ACK_KEY) ?? null
}

export function hasAckedUrlPasswordRisk(): boolean {
  return !!getStoredAck()
}

export function persistUrlPasswordRiskAck(at = new Date().toISOString()): void {
  storage()?.setItem(URL_PASSWORD_ACK_KEY, at)
}

export function clearUrlPasswordRiskAck(): void {
  storage()?.removeItem(URL_PASSWORD_ACK_KEY)
}

/**
 * 是否需要弹出知情确认。
 * - disabled：不弹确认（调用方应直接拒绝密码启动）
 * - always-ack：每次都要
 * - intranet：仅首次（无 ack 记录时）
 */
export function needsUrlPasswordAck(mode: UrlPasswordMode, hasAcked = hasAckedUrlPasswordRisk()): boolean {
  if (mode === 'disabled') return false
  if (mode === 'always-ack') return true
  return !hasAcked
}

export function isUrlPasswordDisabled(mode: UrlPasswordMode): boolean {
  return mode === 'disabled'
}
