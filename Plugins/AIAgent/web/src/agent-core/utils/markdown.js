/**
 * 极简 Markdown 渲染（安全优先 + 块级排版）。
 *
 * 历史坑（2026-10-26 现场截图暴露）：
 *   旧实现是「整段转义 → 正则替换行内标记 → 把每个 \n 换成 <br />」，
 *   它**没有块级概念**，因此：
 *     ① `### 标题` 原样显示成纯文本（模型爱写标题，天天可见）；
 *     ② 每个空行都变成一个 <br />，块间距不可控 —— 标题与表格之间缝很大；
 *     ③ 更糟的是先 out.join('\n') 再全局替换 \n，等于**把 <br /> 塞进 <tr> 之间
 *        和 </table> 之后**，浏览器把它挑到表格外面，表格上下凭空多出空行。
 *
 * 现在的结构：逐行状态机产出**块级元素**（段落 / 标题 / 列表 / 引用 / 表格 / 代码 / 分割线），
 * 只有**段落内部**的软换行才变成 <br />。块间距交给 CSS 的 margin 统一管，
 * 换行不再兼职排版 —— 这是本次修复的核心。
 *
 * 安全性（不可省）：模型输出属于不可信内容。本模块**先转义再套白名单标签**，
 * 不存在 HTML 注入面；链接只放行 http/https/mailto/相对/锚点。
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

const FENCE_RE = /^\s*(```|~~~)/
const HEADING_RE = /^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$/
const HR_RE = /^\s{0,3}([-*_])\s*(?:\1\s*){2,}$/
const UL_RE = /^\s{0,3}[-*+]\s+(.*)$/
const OL_RE = /^\s{0,3}\d+[.)]\s+(.*)$/
const QUOTE_RE = /^\s{0,3}>\s?(.*)$/

/** 管道表格的一行：`| a | b |` → ['a','b']；不是表格行则返回 null。 */
function parseTableRow(line) {
  const m = /^\s*\|(.+)\|\s*$/.exec(line)
  if (!m) return null
  return m[1].split('|').map((c) => c.trim())
}

/** 分隔行：`| --- | :--: |`，去掉所有竖线后只由 - : 空格组成且至少一个 -。 */
function isTableDelimiter(line) {
  const t = line.trim().replace(/\|/g, '')
  return /-/.test(t) && /^[\s:-]+$/.test(t)
}

/**
 * 行内标记：代码 → 链接 → 粗体 → 斜体。
 *
 * 行内代码先抽成占位符，避免其中的 `*` `_` `[` 被后续规则改写 ——
 * 否则 `a * b` 这类代码片段会被吃成斜体。
 */
function inline(raw) {
  let html = escapeHtml(raw)
  const codes = []
  html = html.replace(/`([^`\n]+)`/g, (_, code) => {
    codes.push(code)
    return `\u0000${codes.length - 1}\u0000`
  })
  html = html.replace(
    /\[([^\]\n]+)\]\(([^)\s]+)\)/g,
    (_, label, url) =>
      `<a class="aiw-link" href="${escapeHtml(safeUrl(url))}" target="_blank" rel="noopener noreferrer">${label}</a>`
  )
  html = html.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
  html = html.replace(/__([^_\n]+)__/g, '<strong>$1</strong>')
  html = html.replace(/(^|[\s(])\*([^*\n]+)\*(?=[\s).,!?]|$)/g, '$1<em>$2</em>')
  html = html.replace(
    /\u0000(\d+)\u0000/g,
    (_, idx) => `<code class="aiw-inline-code">${codes[Number(idx)]}</code>`
  )
  return html
}

export function renderMarkdown(input) {
  if (!input) return ''
  const lines = String(input).replace(/\r\n?/g, '\n').split('\n')
  const out = []

  /* 段落累积器：连续普通行攒成一段，只有段内的软换行才转 <br /> */
  let para = []
  const flushPara = () => {
    if (!para.length) return
    out.push(`<p class="aiw-p">${para.map(inline).join('<br />')}</p>`)
    para = []
  }

  let i = 0
  while (i < lines.length) {
    const line = lines[i]

    /* ---- 代码围栏 ---- */
    const fence = FENCE_RE.exec(line)
    if (fence) {
      flushPara()
      const marker = fence[1]
      const buf = []
      i++
      while (i < lines.length && !lines[i].trim().startsWith(marker)) {
        buf.push(lines[i])
        i++
      }
      i++ /* 跳过收尾围栏（流式期间可能没有，跳过越界即可） */
      const first = (buf[0] || '').trim()
      const lang = buf.length > 1 && /^[a-z0-9+#.-]+$/i.test(first) ? buf.shift().trim() : ''
      out.push(
        `<pre class="aiw-code"${lang ? ` data-lang="${escapeHtml(lang)}"` : ''}><code>${escapeHtml(
          buf.join('\n')
        )}</code></pre>`
      )
      continue
    }

    /* ---- 标题：用 div + role=heading，避免被宿主 reset 掉 h1~h6 的字号/字重 ---- */
    const heading = HEADING_RE.exec(line)
    if (heading) {
      flushPara()
      const level = heading[1].length
      out.push(
        `<div class="aiw-h aiw-h${level}" role="heading" aria-level="${level}">${inline(heading[2])}</div>`
      )
      i++
      continue
    }

    /* ---- 分割线（必须早于列表判断：`- - -` 也能匹配列表） ---- */
    if (HR_RE.test(line)) {
      flushPara()
      out.push('<hr class="aiw-hr" />')
      i++
      continue
    }

    /* ---- 表格：表头行 + 紧随的分隔行才算表格起点 ---- */
    const row = parseTableRow(line)
    if (row && i + 1 < lines.length && isTableDelimiter(lines[i + 1])) {
      flushPara()
      const header = row
      i += 2
      const body = []
      while (i < lines.length) {
        const r = parseTableRow(lines[i])
        if (!r) break
        if (!isTableDelimiter(lines[i])) body.push(r)
        i++
      }
      const cols = header.length
      const head = header.map((c) => `<th>${inline(c)}</th>`).join('')
      /* 按表头列数补齐：模型偶尔少写一列，缺的补空单元格而不是让整行错位 */
      const rows = body
        .map(
          (r) =>
            `<tr>${Array.from({ length: cols }, (_, c) => `<td>${inline(r[c] ?? '')}</td>`).join('')}</tr>`
        )
        .join('')
      out.push(
        `<div class="aiw-table-wrap"><table class="aiw-table"><thead><tr>${head}</tr></thead><tbody>${rows}</tbody></table></div>`
      )
      continue
    }

    /* ---- 引用：连续多行合并成一个 blockquote ---- */
    if (QUOTE_RE.test(line)) {
      flushPara()
      const buf = []
      while (i < lines.length) {
        const q = QUOTE_RE.exec(lines[i])
        if (!q) break
        buf.push(q[1])
        i++
      }
      out.push(`<blockquote class="aiw-quote">${buf.map(inline).join('<br />')}</blockquote>`)
      continue
    }

    /* ---- 列表：同类型连续行合并；遇到其他类型立即换标签 ---- */
    const ul = UL_RE.exec(line)
    const ol = OL_RE.exec(line)
    if (ul || ol) {
      flushPara()
      const isUl = !!ul
      const re = isUl ? UL_RE : OL_RE
      const tag = isUl ? 'ul' : 'ol'
      const items = []
      while (i < lines.length) {
        const m = re.exec(lines[i])
        if (!m) break
        items.push(m[1])
        i++
      }
      out.push(
        `<${tag} class="aiw-${tag}">${items.map((t) => `<li>${inline(t)}</li>`).join('')}</${tag}>`
      )
      continue
    }

    /* ---- 空行：块分隔，不产出任何高度 ---- */
    if (!line.trim()) {
      flushPara()
      i++
      continue
    }

    /* ---- 普通行：攒进段落 ---- */
    para.push(line)
    i++
  }

  flushPara()
  return out.join('')
}

export default renderMarkdown
