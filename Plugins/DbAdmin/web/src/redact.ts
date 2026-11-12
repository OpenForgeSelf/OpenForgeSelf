/**
 * 前端防御性脱敏：服务端已返回 `****`，这里对展示文本再兜一层，保证页面搜不到明文（S7）。
 * 规则对齐 04§5：password= / pwd= / Bearer / URI 里的 user:pass@ → `****`。
 */
export const MASK = '****'

const KV_SECRET = /\b(password|pwd|passwd|secret|token|api[_-]?key)\s*=\s*('[^']*'|"[^"]*"|[^;\s,)]*)/gi
const BEARER = /\bBearer\s+[A-Za-z0-9._~+/=-]+/g
const URI_CRED = /([a-z][a-z0-9+.-]*:\/\/[^:/\s@]+):([^@\s/]+)@/gi

export function redactText(input: string | null | undefined): string {
  if (!input) return ''
  return input
    .replace(KV_SECRET, (m, k: string, v: string) => (v.replace(/^['"]|['"]$/g, '') === MASK ? m : `${k}=${MASK}`))
    .replace(BEARER, `Bearer ${MASK}`)
    .replace(URI_CRED, (_m, head: string) => `${head}:${MASK}@`)
}

/** 掩码连接串（连接列表 / 表单只读回显） */
export function maskConnStr(connStr: string | null | undefined): string {
  return redactText(connStr)
}

/** 密码框回显：有值即 `****`，无值空串 */
export function maskPassword(v: string | null | undefined): string {
  return v ? MASK : ''
}

/** 检测文本中是否还有「键=非掩码值」形态的明文密钥（单测与 CT 共用判据） */
export function hasPlaintextSecret(text: string): boolean {
  const re = /\b(password|pwd|passwd)\s*=\s*('[^']*'|"[^"]*"|[^;\s,)]*)/gi
  let m: RegExpExecArray | null
  while ((m = re.exec(text))) {
    const v = m[2].replace(/^['"]|['"]$/g, '')
    if (v && v !== MASK) return true
  }
  return false
}
