/**
 * 审计 / UI 展示侧脱敏：env 疑似密钥只留键名 + ***；顺带脱敏常见明文形态。
 * 与后端 SecretRedactor 口径对齐（展示层双保险，不替代服务端脱敏）。
 */

const SENSITIVE_ENV_KEY = /(?:password|passwd|pwd|secret|token|api[_-]?key|credential|private[_-]?key|access[_-]?key|client[_-]?secret|sshpass)/i

/** 值形态像密钥（长随机串 / PEM / Bearer） */
function looksLikeSecretValue(v: string): boolean {
  if (!v) return false
  if (/BEGIN [A-Z ]*PRIVATE KEY/.test(v)) return true
  if (/^Bearer\s+\S+/i.test(v)) return true
  if (v.length >= 24 && /^[A-Za-z0-9+/=_\-.]{24,}$/.test(v)) return true
  return false
}

export function isSensitiveEnvKey(key: string): boolean {
  return SENSITIVE_ENV_KEY.test(key.trim())
}

/** 将 env 对象转为审计展示用脱敏摘要 */
export function redactEnvForDisplay(env: Record<string, string> | undefined | null): Record<string, string> | undefined {
  if (!env || typeof env !== 'object') return undefined
  const out: Record<string, string> = {}
  for (const [k, v] of Object.entries(env)) {
    if (isSensitiveEnvKey(k) || looksLikeSecretValue(String(v ?? ''))) {
      out[k] = '***'
    } else {
      out[k] = String(v ?? '')
    }
  }
  return out
}

/** 对任意审计 detail / 文本做展示脱敏（含 env.KEY=value、KEY=value） */
export function redactAuditText(text: string | undefined | null): string {
  if (!text) return ''
  let result = text
  result = result.replace(/\bBearer\s+[A-Za-z0-9\-._~+/]+=*/gi, 'Bearer ***REDACTED***')
  result = result.replace(/\b(password|passwd|pwd)\s*[=:]\s*[^\s"']+/gi, '$1=***REDACTED***')
  result = result.replace(/\b(token|access_token|refresh_token|auth_token)\s*[=:]\s*[^\s"']+/gi, '$1=***REDACTED***')
  result = result.replace(/\b(api[_-]?key|secret[_-]?key|client[_-]?secret)\s*[=:]\s*[^\s"']+/gi, '$1=***REDACTED***')
  result = result.replace(/\bSSHPASS\s*=\s*\S+/gi, 'SSHPASS=***REDACTED***')
  // env 形态：env.SECRET_KEY=xxx 或 "SECRET_TOKEN":"xxx"
  result = result.replace(
    /\b(env\.)?([A-Za-z0-9_.-]*(?:password|passwd|pwd|secret|token|api[_-]?key|credential|private[_-]?key)[A-Za-z0-9_.-]*)\s*[=:]\s*("[^"]*"|'[^']*'|[^\s,;|&]+)/gi,
    (_m, envPrefix: string | undefined, key: string) => `${envPrefix || ''}${key}=***`,
  )
  return result
}
