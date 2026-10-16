/**
 * 断点续传去重过滤器（v1.7 / Q-09）。
 *
 * 场景：流式响应中途断网，前端带 `Last-Event-ID` 重连。后端此时有两种行为——
 *   A. **支持续传**：只推断点之后的新内容 → 整段都是新的，原样透传。
 *   B. **不支持续传**：从头重放 → 前面一截是「已经显示给用户的内容」，
 *      直接吐出去会让用户看到「前半前半后半」这种重复。
 *
 * 这个过滤器把两种情况统一：先憋住新流的前 `committed.length` 个字符做前缀比对，
 * 匹配上（B）就只吐尾部；匹配不上（A）就整段吐。憋住那一段是为了避免
 * 重复内容在界面上闪一下——宁可晚 1 帧，也不要闪。
 *
 * 纯函数、无外部依赖，因此能被验收器独立断言（不依赖任何 DOM / 网络）。
 *
 * @param {string} committed 断流前已经显示给用户的文本
 */
export function createResumeFilter(committed = '') {
  const known = String(committed ?? '')
  const need = known.length
  let buf = ''
  // 已知前缀为空时无需比对，直接透传（避免每次连接都白憋一拍）
  let matched = need === 0
  let emitted = 0

  return {
    /** 传入一片新到达的文本，返回真正可以追加到界面的部分（可能为空串） */
    push(chunk) {
      buf += String(chunk ?? '')
      if (!matched) {
        if (buf.length < need) return '' // 前缀还没到齐，先憋住
        if (buf.startsWith(known)) {
          matched = true
          emitted = need // B：后端从头重放，跳过已展示的前缀
        } else {
          matched = true
          emitted = 0 // A：后端只推了新内容，整段都要
        }
      }
      if (buf.length > emitted) {
        const out = buf.slice(emitted)
        emitted = buf.length
        return out
      }
      return ''
    },
    /** 是否已越过前缀判定点 */
    get resumed() {
      return matched
    },
    /** 是否检测到「后端从头重放」（供调试 / 审计） */
    get replayed() {
      return matched && need > 0 && buf.startsWith(known)
    },
    get buffered() {
      return buf
    },
  }
}

export default { createResumeFilter }
