/**
 * SmartTerminal URL 深链协议解析与地址栏脱敏。
 *
 * 触发：`st=1` 或 `st_launch=1`（兼容无前缀短参）。
 * 示例见 docs/03-URL协议.md。
 */
import type { LaunchTerminalRequest, LaunchType } from './types'

const SENSITIVE_KEYS = new Set([
  'password', 'pwd', 'passwd', 'st_password', 'st_pwd', 'pass', 'ssh_password',
])

export interface ParsedLaunch {
  request: LaunchTerminalRequest
  /** 解析前是否在 query 中看到密码键 */
  passwordInUrl: boolean
  /** 供 UI 展示的摘要（已脱敏） */
  summary: string
  /** 当次策略覆盖（来自 st_url_password_mode） */
  urlPasswordMode?: string
}

function pick(params: URLSearchParams, ...keys: string[]): string | undefined {
  for (const k of keys) {
    const v = params.get(k)
    if (v != null && v !== '') return v
  }
  return undefined
}

function allCmds(params: URLSearchParams): string[] {
  const multi = params.getAll('cmd').concat(params.getAll('st_cmd'))
  const csv = pick(params, 'cmds', 'st_cmds')
  const fromCsv = csv
    ? csv.split(',').map(s => s.trim()).filter(Boolean)
    : []
  return [...multi, ...fromCsv]
}

export function isLaunchQuery(params: URLSearchParams): boolean {
  const flag = pick(params, 'st', 'st_launch', 'launch')
  if (flag === '1' || flag === 'true' || flag === 'yes') return true
  // 也允许仅有 type=ssh|local|wsl-ssh 时触发
  const type = pick(params, 'st_type', 'type')
  return type === 'local' || type === 'ssh' || type === 'wsl-ssh' || type === 'shell'
}

export function parseLaunchQuery(search: string): ParsedLaunch | null {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  if (!isLaunchQuery(params)) return null

  const passwordInUrl = [...params.keys()].some(k => SENSITIVE_KEYS.has(k.toLowerCase()))
  const typeRaw = (pick(params, 'st_type', 'type') || 'local').toLowerCase()
  const type: LaunchType =
    typeRaw === 'wsl-ssh' || typeRaw === 'wslssh' ? 'wsl-ssh'
      : typeRaw === 'ssh' ? 'ssh'
        : 'local'

  const portStr = pick(params, 'st_port', 'port')
  const port = portStr ? Number(portStr) : undefined

  const request: LaunchTerminalRequest = {
    type,
    shellId: pick(params, 'st_shell', 'shell'),
    cwd: pick(params, 'st_cwd', 'cwd'),
    title: pick(params, 'st_title', 'title'),
    host: pick(params, 'st_host', 'host'),
    port: Number.isFinite(port) ? port : undefined,
    user: pick(params, 'st_user', 'user'),
    password: pick(params, 'st_password', 'password', 'pwd', 'passwd', 'pass'),
    auth: pick(params, 'st_auth', 'auth') as LaunchTerminalRequest['auth'],
    identityFile: pick(params, 'st_identityFile', 'identityFile', 'identity', 'key'),
    commands: allCmds(params),
  }

  const summaryParts = [`type=${type}`]
  if (request.shellId) summaryParts.push(`shell=${request.shellId}`)
  if (request.cwd) summaryParts.push(`cwd=${request.cwd}`)
  if (request.host) summaryParts.push(`host=${request.host}`)
  if (request.user) summaryParts.push(`user=${request.user}`)
  if (request.port) summaryParts.push(`port=${request.port}`)
  if (request.auth) summaryParts.push(`auth=${request.auth}`)
  if (passwordInUrl) summaryParts.push('password=***REDACTED***')
  if (request.commands?.length) summaryParts.push(`cmds=${request.commands.length}`)

  const urlPasswordMode = pick(params, 'st_url_password_mode', 'url_password_mode')
  return { request, passwordInUrl, summary: summaryParts.join(' · '), urlPasswordMode }
}

/** 从当前地址栏移除敏感 query，并去掉一次性 launch 触发标记（避免刷新重复启动）。 */
export function scrubLaunchParamsFromUrl(href = typeof location !== 'undefined' ? location.href : ''): string {
  try {
    const url = new URL(href, 'http://local.invalid')
    let changed = false
    for (const key of [...url.searchParams.keys()]) {
      const lower = key.toLowerCase()
      if (SENSITIVE_KEYS.has(lower) || lower === 'st' || lower === 'st_launch') {
        url.searchParams.delete(key)
        changed = true
      }
    }
    // 保留 type/host 等非敏感参数便于分享；若需要一次一启，调用方也可全清
    if (changed && typeof history !== 'undefined' && typeof location !== 'undefined') {
      const next = `${url.pathname}${url.search}${url.hash}`
      history.replaceState(null, '', next)
      return next
    }
    return `${url.pathname}${url.search}${url.hash}`
  } catch {
    return href
  }
}
