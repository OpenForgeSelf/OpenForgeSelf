// 容错 JSON 序列解析器。
//
// 背景：后端在捕获「流式（SSE）响应」时，会把多个 `chat.completion.chunk`
// 对象直接拼接成一个字符串（无换行、无分隔符，形如 `{...}{...}{...}`）。
// 标准 `JSON.parse` 会在第一个对象之后因「Unexpected token」直接抛错，
// 导致这类记录的详情根本解析不出来。
//
// 本函数按花括号平衡逐个提取顶层 JSON 对象，兼容以下三种形态：
//   1. 单个合法 JSON 对象（非流式 completion / error 对象）
//   2. 多个 JSON 对象直接拼接（流式分片）
//   3. 空 / null / 非 JSON 文本（返回空数组，不抛错）

export function parseJsonSequence(text?: string | null): unknown[] {
  if (!text) return []

  const out: unknown[] = []
  const n = text.length
  let i = 0
  let depth = 0
  let start = -1
  let inStr = false
  let esc = false

  while (i < n) {
    const c = text[i]
    if (inStr) {
      if (esc) esc = false
      else if (c === '\\') esc = true
      else if (c === '"') inStr = false
      i++
      continue
    }
    if (c === '"') {
      inStr = true
      i++
      continue
    }
    if (c === '{') {
      if (depth === 0) start = i
      depth++
    } else if (c === '}') {
      if (depth > 0) depth--
      if (depth === 0 && start >= 0) {
        const slice = text.slice(start, i + 1)
        try {
          out.push(JSON.parse(slice))
        } catch {
          // 跳过残缺片段，继续尝试后续对象
        }
        start = -1
      }
    }
    i++
  }

  // 兜底：若全程未提取到对象（如末尾附带的垃圾字符），尝试整体解析一次
  if (out.length === 0) {
    try {
      return [JSON.parse(text)]
    } catch {
      return []
    }
  }
  return out
}

// 便捷封装：取序列中的第一个对象（请求体恒为单对象时适用）
export function firstJsonObject<T = Record<string, unknown>>(text?: string | null): T | null {
  const seq = parseJsonSequence(text)
  return (seq[0] as T) ?? null
}
