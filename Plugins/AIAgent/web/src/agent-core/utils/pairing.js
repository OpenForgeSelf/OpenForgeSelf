/**
 * 遥控配对工具（v1.8 / Q-10）—— 纯函数，无 DOM 无网络，便于独立断言。
 *
 * 配对码字符集刻意去掉 I / L / O / 0 / 1：这几个字符在屏幕上看二维码、
 * 或人工抄写时最容易认错，而配对码是要被人读和抄的。
 */

export const CODE_CHARSET = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789'

/** 会话 id：小写，只用于区分「哪块屏」，不承担安全职责 */
export function makeSessionId(rand = Math.random) {
  let s = ''
  for (let i = 0; i < 10; i++) s += CODE_CHARSET[Math.floor(rand() * CODE_CHARSET.length)]
  return s.toLowerCase()
}

/** 配对码：默认 6 位。它才是凭证，泄漏等于把输入权交出去 */
export function makeCode(len = 6, rand = Math.random) {
  const n = Math.min(12, Math.max(4, Number(len) || 6))
  let s = ''
  for (let i = 0; i < n; i++) s += CODE_CHARSET[Math.floor(rand() * CODE_CHARSET.length)]
  return s
}

/**
 * 拼出手机端配对地址。
 * 会话与配对码放在 **hash** 里而不是 query：hash 不会随请求发给静态服务器，
 * 因此不会出现在服务器访问日志里——日志泄漏配对码是最常见的低级事故。
 */
export function buildPairUrl(base, session, code) {
  const b = String(base || '').split('#')[0]
  return `${b}#s=${encodeURIComponent(session || '')}&c=${encodeURIComponent(code || '')}`
}

/** 从配对地址里取回会话与配对码 */
export function parsePairUrl(url) {
  const out = { session: '', code: '' }
  const m = /#(.*)$/.exec(String(url || ''))
  if (!m) return out
  for (const kv of m[1].split('&')) {
    const i = kv.indexOf('=')
    if (i < 0) continue
    const k = kv.slice(0, i)
    const v = decodeURIComponent(kv.slice(i + 1))
    if (k === 's') out.session = v
    else if (k === 'c') out.code = v
  }
  return out
}

/** 远端文本清洗：去首尾空白 + 截断到上限（截断而非拒绝，避免整条丢失） */
export function clampText(t, max = 2000) {
  const s = String(t ?? '').trim()
  const n = Math.max(1, Number(max) || 2000)
  return s.length > n ? s.slice(0, n) : s
}

/** 配对码脱敏：审计日志要能定位是哪一次配对，但**不该**留下完整凭证 */
export function maskCode(code) {
  const s = String(code || '')
  if (s.length <= 2) return '***'
  return s.slice(0, 1) + '*'.repeat(Math.max(1, s.length - 3)) + s.slice(-2)
}

/** 遥控消息 id：手机端生成为主，缺失时组件兜底补一个，保证回执能对上号 */
export function makeRemoteId(rand = Math.random) {
  return 'r' + Date.now().toString(36) + Math.floor(rand() * 46655).toString(36) + CODE_CHARSET[Math.floor(rand() * CODE_CHARSET.length)]
}

export default { CODE_CHARSET, makeSessionId, makeCode, makeRemoteId, buildPairUrl, parsePairUrl, clampText, maskCode }
