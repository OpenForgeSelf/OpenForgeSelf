/* FR-220 会话导出（Markdown）—— 纯函数，不碰 DOM。
   宿主在 Node 里也能直接复用（例如服务端存档 / 邮件摘要）。 */

const ROLE = { user: '用户', assistant: '助手', system: '系统' }

const oneLine = (s) => String(s == null ? '' : s).replace(/\s+/g, ' ').trim()
const cell = (s) => oneLine(s).replace(/\|/g, '\\|') || ' '

function tableBlock(b) {
  const cols = Array.isArray(b.columns) ? b.columns : []
  const rows = Array.isArray(b.rows) ? b.rows : []
  if (!cols.length) return ''
  const out = ['| ' + cols.map(cell).join(' | ') + ' |', '| ' + cols.map(() => '---').join(' | ') + ' |']
  rows.forEach((r) => out.push('| ' + (Array.isArray(r) ? r : [r]).map(cell).join(' | ') + ' |'))
  return out.join('\n')
}

/* 图表在 Markdown 里还原不了形状 —— 与其丢一句「有图」了事，不如把数据列出来，
   读的人还能看到数。 */
function chartBlock(b) {
  const data = b.option && Array.isArray(b.option.data) ? b.option.data : []
  const head = '**图表**：' + oneLine(b.title || '未命名') +
    '（' + oneLine((b.option && b.option.type) || 'bar') + '；Markdown 无法还原图形，改列数据）'
  if (!data.length) return head
  return head + '\n\n' + ['| 名称 | 数值 |', '| --- | --- |']
    .concat(data.map((d) => '| ' + cell(d && d.name) + ' | ' + cell(d && d.value) + ' |')).join('\n')
}

function blockToMd(b) {
  if (!b || typeof b !== 'object') return ''
  switch (b.type) {
    case 'text': return String(b.text || '')
    case 'table': return tableBlock(b)
    case 'code': return '```' + oneLine(b.lang || '') + '\n' + String(b.code || '') + '\n```'
    case 'kpi': {
      const items = Array.isArray(b.items) ? b.items : []
      return items.map((k) => '- **' + oneLine(k.label) + '**：' + oneLine(k.value) + (k.delta ? '（' + oneLine(k.delta) + '）' : '')).join('\n')
    }
    case 'chart': return chartBlock(b)
    case 'citations': {
      const list = Array.isArray(b.items) ? b.items : []
      return list.map((c, i) => (i + 1) + '. [' + oneLine(c.title || c.url) + '](' + oneLine(c.url) + ')' + (c.snippet ? ' —— ' + oneLine(c.snippet) : '')).join('\n')
    }
    case 'image': return '![' + oneLine(b.alt || '图片') + '](' + oneLine(b.url) + ')'
    case 'thinking': return '> **思考过程**' + (b.durationMs ? '（' + Math.round(b.durationMs / 1000) + 's）' : '') + '\n>\n> ' + String(b.text || '').replace(/\n/g, '\n> ')
    case 'plan': {
      const steps = Array.isArray(b.steps) ? b.steps : []
      return steps.map((x) => '- [' + (x && x.done ? 'x' : ' ') + '] ' + oneLine(x && (x.title || x.text))).join('\n')
    }
    case 'tool': {
      const a = b.action || {}
      const args = a.args ? '\n\n入参：\n\n```json\n' + JSON.stringify(a.args, null, 2) + '\n```' : ''
      const res = b.result != null ? '\n\n结果：\n\n```\n' + (typeof b.result === 'string' ? b.result : JSON.stringify(b.result, null, 2)) + '\n```' : ''
      return '**工具**：' + oneLine(b.name || a.name || '未命名') + ' · 状态：' + oneLine(b.status || 'ok') + args + res
    }
    case 'group': {
      const inner = (Array.isArray(b.blocks) ? b.blocks : []).map(blockToMd).filter(Boolean).join('\n\n')
      return '### ' + oneLine(b.title || '步骤') + (inner ? '\n\n' + inner : '')
    }
    case 'approval': return '**审批**：' + oneLine(b.title || '') + ' · 状态：' + oneLine(b.status || '待处理') + (b.impact ? '\n\n影响范围：' + oneLine(b.impact) : '')
    case 'html': return '**HTML 预览**（Markdown 无法还原，已省略）'
    case 'error': return '**错误**：' + oneLine(b.message || '')
    default: return ''
  }
}

/** 单个会话 → Markdown */
export function sessionToMarkdown(session, opts = {}) {
  const s = session || {}
  const msgs = (Array.isArray(s.messages) ? s.messages : []).filter((m) => m && !m.welcome)
  const out = [
    '# ' + oneLine(s.title || '会话'),
    '',
    '- 导出时间：' + new Date(opts.now || Date.now()).toLocaleString('zh-CN'),
    '- 消息数：' + msgs.length,
  ]
  if (s.createdAt) out.push('- 创建时间：' + new Date(s.createdAt).toLocaleString('zh-CN'))
  out.push('')
  for (const m of msgs) {
    out.push('## ' + (ROLE[m.role] || oneLine(m.role) || '消息'))
    if (m.content) out.push('', String(m.content))
    for (const b of (Array.isArray(m.blocks) ? m.blocks : [])) {
      const t = blockToMd(b)
      if (t) out.push('', t)
    }
    out.push('')
  }
  return out.join('\n').replace(/\n{3,}/g, '\n\n').trim() + '\n'
}

/** 多个会话 → 一个 Markdown */
export function sessionsToMarkdown(sessions, opts = {}) {
  return (Array.isArray(sessions) ? sessions : []).map((s) => sessionToMarkdown(s, opts)).join('\n\n---\n\n')
}
