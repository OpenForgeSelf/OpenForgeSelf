/**
 * 消息协议校验与降级 —— 前后端共用同一份契约（见 docs/SCHEMA.md）。
 *
 * 设计原则：**永不抛错**。
 *   后端推过来的数据脏一点是常态（字段拼错、类型漂移、版本不一致），
 *   如果校验失败就抛异常，用户看到的是白屏——比看到一条格式不对的卡片糟糕得多。
 *   所以这里的每个函数都保证返回一个「能渲染」的结果：
 *     合法 → 原样通过
 *     可修复 → 就地补齐（如缺 status 补 'ok'）
 *     不可修复 → 降级为 text 块，把原始内容以可读形式保留下来 + 附一条提示
 *
 * 用法：
 *   import { normalizeMessage } from './utils/schema'
 *   const { message, issues } = normalizeMessage(raw)
 */

/** 已知的块类型 */
export const BLOCK_TYPES = [
  'thinking',
  'tool',
  'plan',
  'text',
  'chart',
  'kpi',
  'table',
  'citations',
  'html',
  'code',
  'image',
  'error',
  'skeleton',
  'approval',
  'group',
]

export const TOOL_STATUSES = ['running', 'ok', 'error', 'denied', 'awaiting']
export const RISK_LEVELS = ['low', 'medium', 'high', 'critical']
export const CHART_TYPES = ['bar', 'line', 'area', 'pie', 'donut']
export const APPROVAL_STATUSES = ['pending', 'approved', 'rejected', 'expired']

const isObj = (v) => v !== null && typeof v === 'object' && !Array.isArray(v)
const isStr = (v) => typeof v === 'string'
const isNum = (v) => typeof v === 'number' && Number.isFinite(v)
const isArr = (v) => Array.isArray(v)

/** 把任意值渲染成可读的一行，用于降级展示 */
function describe(value, limit = 240) {
  let s
  try {
    s = typeof value === 'string' ? value : JSON.stringify(value)
  } catch (e) {
    s = String(value)
  }
  s = String(s ?? '')
  return s.length > limit ? s.slice(0, limit) + '…' : s
}

/**
 * 校验并修复单个块。
 * @returns {{ block: object, issues: string[] }}
 */
export function normalizeBlock(raw, path = 'block') {
  const issues = []

  if (!isObj(raw)) {
    issues.push(`${path}: 不是对象，已降级为文本`)
    return { block: { type: 'text', text: describe(raw) }, issues }
  }

  const type = raw.type
  if (!isStr(type) || !type) {
    issues.push(`${path}: 缺少 type，已降级为文本`)
    return { block: { type: 'text', text: describe(raw) }, issues }
  }

  if (!BLOCK_TYPES.includes(type)) {
    // 未知类型：**不能丢**——新版本后端可能加了新块，老前端要能优雅退化
    issues.push(`${path}: 未知块类型「${type}」，已降级为文本`)
    return {
      block: { type: 'text', text: raw.text || describe(raw), unknownType: type },
      issues,
    }
  }

  const out = { ...raw }

  switch (type) {
    case 'thinking':
      out.done = out.done === undefined ? true : !!out.done
      if (!isNum(out.durationMs)) delete out.durationMs
      if (!isStr(out.text)) out.text = describe(out.text)
      break

    case 'tool':
      if (!isStr(out.name) || !out.name) {
        issues.push(`${path}: tool 缺少 name`)
        out.name = 'unnamed_tool'
      }
      if (!TOOL_STATUSES.includes(out.status)) {
        if (out.status !== undefined) issues.push(`${path}: 非法 tool.status「${out.status}」，已置为 ok`)
        out.status = 'ok'
      }
      if (!isNum(out.durationMs)) delete out.durationMs
      break

    case 'plan':
      if (!isArr(out.steps)) {
        issues.push(`${path}: plan 缺少 steps，已置为空列表`)
        out.steps = []
      } else {
        out.steps = out.steps.map((s, i) => (isObj(s) ? { status: 'pending', ...s } : { title: describe(s), status: 'pending' }))
      }
      break

    case 'chart':
      // 兼容两种写法：option:{type,data} 或顶层 chartType+categories+series
      const opt = isObj(out.option) ? out.option : {}
      if (!isStr(opt.type)) opt.type = isStr(out.chartType) ? out.chartType : 'bar'
      if (!CHART_TYPES.includes(opt.type)) {
        issues.push(`${path}: 未知图表类型「${opt.type}」，已回退 bar`)
        opt.type = 'bar'
      }
      if (!isArr(opt.data)) {
        if (isArr(out.categories) && isArr(out.series)) {
          opt.data = out.categories.map((name, i) => ({ name, value: out.series[i] ?? 0 }))
        } else if (isArr(out.data)) {
          opt.data = out.data
        } else if (isArr(out.values)) {
          opt.data = out.values
        } else {
          issues.push(`${path}: chart 无有效数据，已置为空`)
          opt.data = []
        }
      }
      out.option = opt
      break

    case 'kpi':
      if (!isArr(out.items)) {
        issues.push(`${path}: kpi 缺少 items`)
        out.items = []
      }
      break

    case 'table':
      if (!isArr(out.columns)) {
        issues.push(`${path}: table 缺少 columns`)
        out.columns = []
      }
      if (!isArr(out.rows)) out.rows = []
      break

    case 'citations':
      if (!isArr(out.items)) {
        issues.push(`${path}: citations 缺少 items`)
        out.items = []
      }
      break

    case 'html':
      if (!isStr(out.html)) out.html = describe(out.html)
      break

    case 'code':
      if (!isStr(out.code)) out.code = describe(out.code)
      break

    case 'image':
      if (!isStr(out.url) || !out.url) {
        issues.push(`${path}: image 缺少 url，已降级为文本`)
        return { block: { type: 'text', text: out.alt || '（图片地址缺失）' }, issues }
      }
      break

    case 'error':
      if (!isStr(out.message)) out.message = describe(out.message)
      if (out.retryable === undefined) out.retryable = true
      break

    case 'skeleton':
      if (!isNum(out.rows)) out.rows = 3
      break

    case 'approval': {
      if (out.id === undefined || out.id === null) {
        issues.push(`${path}: approval 缺少 id，无法被 approve/reject 定位`)
        out.id = `auto_${Math.random().toString(36).slice(2, 10)}`
      }
      if (!RISK_LEVELS.includes(out.risk)) {
        if (out.risk !== undefined) issues.push(`${path}: 非法 risk「${out.risk}」，已置为 medium`)
        out.risk = 'medium'
      }
      if (!isStr(out.title)) out.title = describe(out.title)
      if (!isObj(out.action)) {
        issues.push(`${path}: approval 缺少 action`)
        out.action = { name: out.actionName || 'unknown_action', args: {} }
      } else if (!isStr(out.action.name)) {
        out.action = { ...out.action, name: 'unknown_action' }
      }
      if (!isNum(out.timeoutMs)) delete out.timeoutMs
      if (!isNum(out.createdAt)) out.createdAt = Date.now()
      if (out.status !== undefined && !APPROVAL_STATUSES.includes(out.status)) {
        issues.push(`${path}: 非法 approval.status「${out.status}」，已置为 pending`)
        out.status = 'pending'
      }
      break
    }

    case 'group': {
      if (!isArr(out.blocks)) {
        issues.push(`${path}: group 缺少 blocks，已置为空列表`)
        out.blocks = []
      }
      const nested = normalizeBlocks(out.blocks, `${path}.blocks`)
      out.blocks = nested.blocks
      issues.push(...nested.issues)
      if (!isStr(out.status)) out.status = 'done'
      if (out.collapsed === undefined) out.collapsed = false
      break
    }

    default:
      break
  }

  return { block: out, issues }
}

/** 批量校验块列表 */
export function normalizeBlocks(list) {
  if (!isArr(list)) return { blocks: [], issues: ['blocks: 不是数组'] }
  const issues = []
  const blocks = list.map((b, i) => {
    const r = normalizeBlock(b, `blocks[${i}]`)
    issues.push(...r.issues)
    return r.block
  })
  return { blocks, issues }
}

/**
 * 校验一整条消息。
 * @returns {{ message: object, issues: string[] }}
 */
export function normalizeMessage(raw) {
  if (!isObj(raw)) {
    return { message: { role: 'assistant', content: describe(raw) }, issues: ['message: 不是对象'] }
  }
  const issues = []
  const out = { ...raw }
  if (out.role !== 'user' && out.role !== 'assistant') {
    if (out.role !== undefined) issues.push(`message: 非法 role「${out.role}」，已置为 assistant`)
    out.role = 'assistant'
  }
  if (!isStr(out.content)) out.content = out.content == null ? '' : describe(out.content)
  if (out.blocks !== undefined) {
    const r = normalizeBlocks(out.blocks)
    out.blocks = r.blocks
    issues.push(...r.issues)
  }
  return { message: out, issues }
}

export default { normalizeBlock, normalizeBlocks, normalizeMessage, BLOCK_TYPES }
