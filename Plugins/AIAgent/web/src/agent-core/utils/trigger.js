/**
 * 触发判定（`/` 命令 / `@` 提及共用的纯逻辑）。
 *
 * 抽出来是为了可测 —— 组件里的键盘与状态编排不便于单测，而「什么时候该弹面板」
 * 恰恰是最容易写错又最影响手感的一段（弹错地方会疯狂打扰）。
 */

/**
 * 判定光标前文本是否处于触发态。
 *
 * ★ 关键：**行首或前一个字符是空白**才算触发。
 *   否则用户打路径 `src/api/foo`、邮箱 `a@b.com`、比例 `1/2` 时，面板会一路弹出来。
 *
 * @param {string} before 光标之前的文本
 * @param {string[]} chars 宿主注册的触发字符（如 `['/', '@']`）
 * @returns {{ char: string, query: string, start: number } | null}
 *          start = 触发字符在 before 中的下标（用于替换整段）
 */
export function matchTrigger(before, chars) {
  const text = String(before ?? '')
  if (!text || !Array.isArray(chars) || !chars.length) return null
  const m = text.match(/(^|\s)([^\s])([^\s]*)$/)
  if (!m) return null
  if (!chars.includes(m[2])) return null
  const query = m[3] ?? ''
  return { char: m[2], query, start: text.length - query.length - 1 }
}

/**
 * 候选项过滤：名称 / 描述 / 附加信息任一命中即可。
 * 与组件 ComposeTrigger 内部保持一致（组件那边也用同一套字段）。
 *
 * @param {Array<{name?: string, desc?: string, meta?: string}>} items
 * @param {string} query
 * @param {number} limit 每组最多返回多少条（默认 50，防 112 个工具一次性铺满）
 */
export function filterItems(items, query, limit = 50) {
  const list = Array.isArray(items) ? items : []
  const q = String(query ?? '').trim().toLowerCase()
  const hit = q
    ? list.filter((it) => [it.name, it.desc, it.meta].some((v) => String(v ?? '').toLowerCase().includes(q)))
    : list.slice()
  return hit.slice(0, limit)
}
