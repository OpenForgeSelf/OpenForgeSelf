/**
 * SmartTerminal 通用 URL 启动器解析与地址栏脱敏。
 *
 * LaunchSpec：profile + shell/cwd/title/env + step|run + auth…
 * 详见 docs/03-URL协议.md。
 */
import type { LaunchStep, LaunchTerminalRequest, LaunchType } from './types'

const SENSITIVE_KEYS = new Set([
  'password', 'pwd', 'passwd', 'st_password', 'st_pwd', 'pass', 'ssh_password',
])

const AUTH_PROFILES = new Set(['ssh', 'wsl-ssh'])

export interface ParsedLaunch {
  request: LaunchTerminalRequest
  /** 解析前是否在 query 中看到密码键 */
  passwordInUrl: boolean
  /** 无 URL 密码且 profile 需要认证、非 key → 应弹出交互密码框 */
  needsInteractivePassword: boolean
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

function parseEnv(params: URLSearchParams): Record<string, string> | undefined {
  const env: Record<string, string> = {}
  for (const [k, v] of params.entries()) {
    if (k.startsWith('env.') && k.length > 4) {
      env[k.slice(4)] = v
    }
  }
  const csv = pick(params, 'env', 'st_env')
  if (csv) {
    for (const part of csv.split(',')) {
      const i = part.indexOf(':')
      const j = part.indexOf('=')
      const sep = i >= 0 && (j < 0 || i < j) ? i : j
      if (sep > 0) {
        const key = part.slice(0, sep).trim()
        const val = part.slice(sep + 1).trim()
        if (key) env[key] = val
      }
    }
  }
  return Object.keys(env).length ? env : undefined
}

function quoteArg(a: string): string {
  if (/^[A-Za-z0-9_./:@%+=,-]+$/.test(a)) return a
  return `'${a.replace(/'/g, `'\\''`)}'`
}

function flattenStep(step: LaunchStep): string {
  const cmd = (step.command || '').trim()
  const args = step.args?.filter(a => a != null && String(a).length) ?? []
  if (!args.length) return cmd
  return [cmd, ...args.map(a => quoteArg(String(a)))].filter(Boolean).join(' ')
}

function parseRunJson(raw: string | undefined): LaunchStep[] {
  if (!raw) return []
  try {
    const data = JSON.parse(raw)
    if (!Array.isArray(data)) return []
    return data
      .map((item): LaunchStep | null => {
        if (typeof item === 'string') return { command: item }
        if (item && typeof item === 'object' && typeof (item as LaunchStep).command === 'string') {
          const o = item as LaunchStep
          return {
            command: o.command,
            args: Array.isArray(o.args) ? o.args.map(String) : undefined,
          }
        }
        return null
      })
      .filter((x): x is LaunchStep => !!x && !!x.command?.trim())
  } catch {
    return []
  }
}

function allSteps(params: URLSearchParams): { steps: LaunchStep[]; commands: string[] } {
  const runRaw = pick(params, 'run', 'st_run')
  const fromRun = parseRunJson(runRaw)
  if (fromRun.length) {
    return { steps: fromRun, commands: fromRun.map(flattenStep).filter(Boolean) }
  }

  const multi = [
    ...params.getAll('step'),
    ...params.getAll('st_step'),
    ...params.getAll('cmd'),
    ...params.getAll('st_cmd'),
  ].map(s => s.trim()).filter(Boolean)

  const csv = pick(params, 'cmds', 'st_cmds')
  const fromCsv = csv
    ? csv.split(',').map(s => s.trim()).filter(Boolean)
    : []

  const lines = [...multi, ...fromCsv]
  const steps = lines.map(command => ({ command }))
  return { steps, commands: lines }
}

function normalizeProfile(raw: string | undefined): { profile: string; type: LaunchType } {
  const p = (raw || 'local').trim().toLowerCase()
  if (p === 'wsl-ssh' || p === 'wslssh') return { profile: 'wsl-ssh', type: 'wsl-ssh' }
  if (p === 'ssh') return { profile: 'ssh', type: 'ssh' }
  if (p === 'shell' || p === 'local') return { profile: 'local', type: 'local' }
  if (p.startsWith('custom:')) return { profile: p, type: 'local' }
  return { profile: p || 'local', type: 'local' }
}

export function isLaunchQuery(params: URLSearchParams): boolean {
  const flag = pick(params, 'st', 'st_launch', 'launch')
  if (flag === '1' || flag === 'true' || flag === 'yes') return true
  const profile = pick(params, 'st_profile', 'profile', 'st_type', 'type')
  if (!profile) return false
  const lower = profile.toLowerCase()
  return (
    lower === 'local'
    || lower === 'ssh'
    || lower === 'wsl-ssh'
    || lower === 'wslssh'
    || lower === 'shell'
    || lower.startsWith('custom:')
  )
}

export function parseLaunchQuery(search: string): ParsedLaunch | null {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  if (!isLaunchQuery(params)) return null

  const passwordInUrl = [...params.keys()].some(k => SENSITIVE_KEYS.has(k.toLowerCase()))
  const { profile, type } = normalizeProfile(pick(params, 'st_profile', 'profile', 'st_type', 'type'))
  const { steps, commands } = allSteps(params)
  const env = parseEnv(params)

  const portStr = pick(params, 'st_port', 'port')
  const port = portStr ? Number(portStr) : undefined
  const password = pick(params, 'st_password', 'password', 'pwd', 'passwd', 'pass')
  let auth = pick(params, 'st_auth', 'auth') as LaunchTerminalRequest['auth'] | undefined

  const needsAuth = AUTH_PROFILES.has(type) || AUTH_PROFILES.has(profile)
  if (needsAuth && !auth) {
    auth = password || passwordInUrl ? 'sshpass' : 'interactive'
  }

  const needsInteractivePassword =
    needsAuth
    && !passwordInUrl
    && !password
    && (auth === 'interactive' || !auth || auth === 'password')

  const request: LaunchTerminalRequest = {
    profile,
    type,
    shellId: pick(params, 'st_shell', 'shell'),
    cwd: pick(params, 'st_cwd', 'cwd'),
    title: pick(params, 'st_title', 'title'),
    host: pick(params, 'st_host', 'host'),
    port: Number.isFinite(port) ? port : undefined,
    user: pick(params, 'st_user', 'user'),
    password,
    auth,
    identityFile: pick(params, 'st_identityFile', 'identityFile', 'identity', 'key'),
    env,
    steps: steps.length ? steps : undefined,
    commands: commands.length ? commands : undefined,
  }

  const summaryParts = [`profile=${profile}`]
  if (request.shellId) summaryParts.push(`shell=${request.shellId}`)
  if (request.cwd) summaryParts.push(`cwd=${request.cwd}`)
  if (request.title) summaryParts.push(`title=${request.title}`)
  if (request.host) summaryParts.push(`host=${request.host}`)
  if (request.user) summaryParts.push(`user=${request.user}`)
  if (request.port) summaryParts.push(`port=${request.port}`)
  if (request.auth) summaryParts.push(`auth=${request.auth}`)
  else if (needsInteractivePassword) summaryParts.push('auth=interactive')
  if (request.env) summaryParts.push(`env=${Object.keys(request.env).length}`)
  if (passwordInUrl) summaryParts.push('password=***REDACTED***')
  if (commands.length) summaryParts.push(`steps=${commands.length}`)

  const urlPasswordMode = pick(params, 'st_url_password_mode', 'url_password_mode')
  return {
    request,
    passwordInUrl,
    needsInteractivePassword,
    summary: summaryParts.join(' · '),
    urlPasswordMode,
  }
}

/** 从当前地址栏移除敏感 query，并去掉一次性 launch 触发标记（避免刷新重复启动）。 */
export function scrubLaunchParamsFromUrl(href = typeof location !== 'undefined' ? location.href : ''): string {
  try {
    const url = new URL(href, 'http://local.invalid')
    let changed = false
    for (const key of [...url.searchParams.keys()]) {
      const lower = key.toLowerCase()
      if (SENSITIVE_KEYS.has(lower) || lower === 'st' || lower === 'st_launch' || lower === 'launch') {
        url.searchParams.delete(key)
        changed = true
      }
    }
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
