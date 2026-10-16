/**
 * 会话历史（v1.12.0）—— 纯函数层。
 *
 * 为什么单独一个文件：会话的**迁移、裁剪、标题推导**全是纯逻辑，
 * 塞进 composable 就只能靠 DOM 间接测（而且一测就是「跑起来才知道对不对」）。
 * 抽到这里可以逐条断言，和 resume.js / pairing.js 一个路子。
 *
 * 存储格式（v1）：
 *   {
 *     "v": 1,
 *     "current": "s1",
 *     "sessions": [ { id, title, createdAt, updatedAt, titleLocked, messages: [...] } ]
 *   }
 *
 * 向后兼容：v1.11 及以前是「一个 storageKey 直接存一段对话的裸数组」。
 * readStore 认得这种旧格式并迁移成第一个会话 —— 老用户升级后不该丢对话。
 */

export const STORE_VERSION = 1
export const DEFAULT_MAX = 20
export const TITLE_MAX = 24
/** 存储键后缀。与老的 storageKey 分开，老数据原地不动，迁移时读一次即可 */
export const STORE_SUFFIX = ':sessions'

/** 把任意值夹到 [1, 100]，非法值回退默认 */
export function clampMax(n, fallback = DEFAULT_MAX) {
  const v = Number(n)
  if (!Number.isFinite(v)) return fallback
  return Math.min(100, Math.max(1, Math.floor(v)))
}

/**
 * 从消息里推一个会话标题：取**第一条用户消息**，压平空白后截断。
 * 助手欢迎语不算 —— 每个新会话都有它，拿来当标题会全是「你好，我是智能助手」。
 */
export function deriveTitle(messages, fallback = '新会话') {
  const list = Array.isArray(messages) ? messages : []
  const first = list.find((m) => m && m.role === 'user' && String(m.content || '').trim())
  if (!first) return fallback
  const flat = String(first.content).replace(/\s+/g, ' ').trim()
  if (!flat) return fallback
  return flat.length > TITLE_MAX ? flat.slice(0, TITLE_MAX) + '…' : flat
}

/** 造一个会话条目。id 由调用方给（组件用 uid()，测试可指定） */
export function makeSession(id, now, messages = []) {
  const t = Number(now) || 0
  const list = Array.isArray(messages) ? messages : []
  return {
    id: String(id),
    title: deriveTitle(list),
    createdAt: t,
    updatedAt: t,
    titleLocked: false,
    messages: list,
  }
}

/** 按最后更新时间倒序（新的在前）—— 侧栏就是这么排的 */
export function sortByUpdated(list) {
  return [...list].sort((a, b) => (b.updatedAt || 0) - (a.updatedAt || 0))
}

/** 裁剪到最多 max 个，保留最近的 */
export function trim(list, max = DEFAULT_MAX) {
  const m = clampMax(max)
  return sortByUpdated(Array.isArray(list) ? list : []).slice(0, m)
}

/** 插入或按 id 替换，然后裁剪。新的/更新的会顶到最前 */
export function upsert(list, session, max = DEFAULT_MAX) {
  const arr = Array.isArray(list) ? list.filter((s) => s && s.id !== session.id) : []
  return trim([session, ...arr], max)
}

/** 按 id 删除 */
export function removeById(list, id, max = DEFAULT_MAX) {
  return trim((Array.isArray(list) ? list : []).filter((s) => s && s.id !== id), max)
}

/**
 * 读存储。**永不抛错** —— 存储里的东西可能是上一版写的、可能是半截的、
 * 也可能被别的代码覆盖过，任何异常都只能降级，不能让组件白屏。
 * @returns {{sessions:Array, current:string|null, migrated:boolean, issues:string[]}}
 */
export function readStore(raw, max = DEFAULT_MAX) {
  const out = { sessions: [], current: null, migrated: false, issues: [] }
  if (!raw) return out

  let data = null
  try {
    data = JSON.parse(raw)
  } catch (e) {
    out.issues.push('json')
    return out
  }

  // 旧格式：裸数组（v1.11 及以前的单会话）→ 迁移成第一个会话
  if (Array.isArray(data)) {
    if (!data.length) {
      out.issues.push('empty-legacy')
      return out
    }
    const s = makeSession('s1', Date.now(), data)
    out.sessions = [s]
    out.current = s.id
    out.migrated = true
    return out
  }

  if (!data || typeof data !== 'object') {
    out.issues.push('shape')
    return out
  }

  const list = Array.isArray(data.sessions) ? data.sessions : []
  const seen = new Set()
  for (const item of list) {
    if (!item || typeof item !== 'object') {
      out.issues.push('item')
      continue
    }
    const id = typeof item.id === 'string' && item.id ? item.id : null
    if (!id || seen.has(id)) {
      // id 缺失或重复：直接丢弃。重复 id 会让「切换会话」切到错误的那一个，
      // 比丢一个会话更糟
      out.issues.push('id')
      continue
    }
    seen.add(id)
    const msgs = Array.isArray(item.messages) ? item.messages : []
    out.sessions.push({
      id,
      title: typeof item.title === 'string' && item.title.trim() ? item.title : deriveTitle(msgs),
      createdAt: Number(item.createdAt) || 0,
      updatedAt: Number(item.updatedAt) || 0,
      titleLocked: !!item.titleLocked,
      messages: msgs,
    })
  }

  out.sessions = trim(out.sessions, max)
  const valid = new Set(out.sessions.map((s) => s.id))
  out.current = typeof data.current === 'string' && valid.has(data.current)
    ? data.current
    : (out.sessions[0] ? out.sessions[0].id : null)
  return out
}

/** 序列化。messages 也一起写 —— 会话历史的意义就是能切回去看 */
/**
 * 合并两个标签页的存储（FR-219）。
 * 同一个会话 id 取 `updatedAt` 更新的一方 —— 「最后写的赢」会直接吞掉对方的内容，
 * 那正是这个需求要解决的问题。current 优先保留本地（本页正在看的那条）。
 */
export function mergeStores(local, incoming, max = DEFAULT_MAX) {
  const byId = new Map()
  const add = (s) => {
    if (!s || !s.id) return
    const cur = byId.get(s.id)
    if (!cur || (s.updatedAt || 0) > (cur.updatedAt || 0)) byId.set(s.id, s)
  }
  ;((local && local.sessions) || []).forEach(add)
  ;((incoming && incoming.sessions) || []).forEach(add)
  const sessions = sortByUpdated(Array.from(byId.values())).slice(0, clampMax(max))
  const has = (id) => !!id && byId.has(id)
  const current = has(local && local.current) ? local.current
    : (has(incoming && incoming.current) ? incoming.current : (sessions[0] ? sessions[0].id : ''))
  return { sessions, current }
}

export function serialize(sessions, current) {
  return JSON.stringify({
    v: STORE_VERSION,
    current: current || null,
    sessions: Array.isArray(sessions) ? sessions : [],
  })
}

/** 相对时间分档。侧栏用它显示「刚刚 / 12 分钟前 / 3 小时前 / 2 天前 / 2026-10-01」 */
export function formatWhen(ts, now = Date.now()) {
  const t = Number(ts) || 0
  if (!t) return ''
  const diff = Math.max(0, (Number(now) || 0) - t)
  if (diff < 60000) return '刚刚'
  const min = Math.floor(diff / 60000)
  if (min < 60) return `${min} 分钟前`
  const hour = Math.floor(min / 60)
  if (hour < 24) return `${hour} 小时前`
  const day = Math.floor(hour / 24)
  if (day < 7) return `${day} 天前`
  const d = new Date(t)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export default {
  STORE_VERSION, DEFAULT_MAX, TITLE_MAX, STORE_SUFFIX,
  clampMax, deriveTitle, makeSession, sortByUpdated, trim, upsert, removeById,
  readStore, serialize, formatWhen,
}
