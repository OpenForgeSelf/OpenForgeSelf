/**
 * 极简 Markdown 渲染（安全优先）：
 * 先整体转义 HTML，再做白名单式的替换，因此不存在 XSS 注入面。
 * 支持：```代码块```、`行内代码`、**粗体**、*斜体*、[链接](url)、- 列表、
 *      GFM 管道表格、换行。
 *
 * 表格为什么必须补：真实 LLM 输出表格是高频场景（实测 7399 现场：模型按要求输出
 * `| 城市 | 人口 |` 三列表格，渲染成纯文本），而 FR-207 最小集没列表格——
 * 契约没覆盖 ≠ 用户不需要。同源候选（标题/引用/分割线）见 TODO。
 */
function escapeHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

function safeUrl(url) {
  const u = String(url).trim()
  return /^(https?:\/\/|mailto:|\/|#)/i.test(u) ? u : '#'
}

/** 管道表格的一行：`| a | b |` → ['a','b']；不是表格行则返回 null。 */
function parseTableRow(line) {
  const m = /^\s*\|(.+)\|\s*$/.exec(line)
  if (!m) return null
  return m[1].split('|').map((c) => c.trim())
}

/** 分隔行：`| --- | :--: |` 这类，去掉所有竖线后只由 - : 空格组成且至少一个 -。 */
function isTableDelimiter(line) {
  const t = line.trim().replace(/\|/g, '')
  return /-/.test(t) && /^[\s:-]+$/.test(t)
}

export function renderMarkdown(input) {
  if (!input) return ''
  const blocks = String(input).split(/```/)

  return blocks
    .map((chunk, i) => {
      // 奇数段是代码块
      if (i % 2 === 1) {
        const lines = chunk.replace(/^\n/, '').replace(/\n$/, '').split('\n')
        const lang = /^[a-z0-9+#-]+$/i.test(lines[0] || '') ? lines.shift() : ''
        return `<pre class="aiw-code"${lang ? ` data-lang="${escapeHtml(lang)}"` : ''}><code>${escapeHtml(
          lines.join('\n')
        )}</code></pre>`
      }
      return renderInline(chunk)
    })
    .join('')
}

function renderInline(text) {
  let html = escapeHtml(text)

  // 行内代码先抽出来占位，避免内部内容被后续规则改写
  const codes = []
  html = html.replace(/`([^`\n]+)`/g, (_, code) => {
    codes.push(code)
    return `\u0000${codes.length - 1}\u0000`
  })

  // 链接
  html = html.replace(/\[([^\]\n]+)\]\(([^)\s]+)\)/g, (_, label, url) => {
    return `<a href="${escapeHtml(safeUrl(url))}" target="_blank" rel="noopener noreferrer">${label}</a>`
  })
  // 粗体 / 斜体
  html = html.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
  html = html.replace(/(^|[\s(])\*([^*\n]+)\*(?=[\s).,!?]|$)/g, '$1<em>$2</em>')

  const lines = html.split('\n')
  const out = []
  let inList = false
  let inTable = false
  let headerCols = 0
  /* 表格状态机：见「表头行 + 分隔行」才算表格起点，避免把普通竖线文本误判成表格。
     渲染成 <table>，列数不一致时按表头列数补空 —— 模型偶发少写一列也不至于错位。 */
  const closeTable = () => {
    if (inTable) {
      out.push('</tbody></table>')
      inTable = false
    }
  }
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const row = parseTableRow(line)
    const nextIsDelim = i + 1 < lines.length && isTableDelimiter(lines[i + 1])

    if (row && (nextIsDelim || inTable)) {
      if (!inTable && nextIsDelim) {
        if (inList) { out.push('</ul>'); inList = false }
        out.push('<table class="aiw-table"><thead><tr>')
        for (const c of row) out.push(`<th>${c}</th>`)
        out.push('</tr></thead><tbody>')
        headerCols = row.length
        inTable = true
        continue // 表头行已消费；下一行（分隔行）由 continue 跳过
      }
      if (inTable) {
        // 分隔行不产出 <tr>
        if (isTableDelimiter(line)) continue
        out.push('<tr>')
        // 按表头列数补齐：模型偶发少写一列时，仍按表头列数渲染，避免列错位
        for (let c = 0; c < headerCols; c++) out.push(`<td>${row[c] ?? ''}</td>`)
        out.push('</tr>')
        continue
      }
    }

    if (inTable) closeTable()
    const m = /^\s*[-*•]\s+(.*)$/.exec(line)
    if (m) {
      if (!inList) {
        out.push('<ul class="aiw-ul">')
        inList = true
      }
      out.push(`<li>${m[1]}</li>`)
    } else {
      if (inList) {
        out.push('</ul>')
        inList = false
      }
      out.push(line)
    }
  }
  closeTable()
  if (inList) out.push('</ul>')

  let result = out.join('\n')
  result = result.replace(/\n/g, '<br />')
  // 还原行内代码
  result = result.replace(/\u0000(\d+)\u0000/g, (_, idx) => `<code class="aiw-inline-code">${codes[Number(idx)]}</code>`)
  return result
}

export default renderMarkdown
