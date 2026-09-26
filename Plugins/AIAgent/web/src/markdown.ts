/**
 * 极简 Markdown 渲染（零依赖），供聊天气泡展示模型输出。
 *
 * 背景：模型回复常带 Markdown（`**粗体**`、代码围栏、列表）与 LaTeX 片段
 * （`$123 \times 456$`），直接当纯文本显示可读性很差。
 *
 * 安全性（重要，不可省）：模型输出属于**不可信内容**，本模块先做 HTML 转义、再套
 * 固定模板标签，最后才交给 v-html；链接只允许 http/https/mailto，杜绝 javascript: 注入。
 */

/** HTML 转义：先把 & < > " ' 变成实体，后续所有替换都基于已转义的安全串。 */
function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

/** 只放行安全协议；其余（尤其 javascript: / data:）退化成纯文本。 */
function safeUrl(url: string): string | null {
  const trimmed = url.trim()
  return /^(https?:|mailto:)/i.test(trimmed) ? trimmed : null
}

/** 常见 LaTeX 记号 → Unicode，提升数学片段可读性（只做高频几个，不追求完备）。 */
function simplifyLatex(s: string): string {
  return s
    .replace(/\\times/g, '×')
    .replace(/\\cdot/g, '·')
    .replace(/\\div/g, '÷')
    .replace(/\\pm/g, '±')
    .replace(/\\leq/g, '≤')
    .replace(/\\geq/g, '≥')
    .replace(/\\neq/g, '≠')
    .replace(/\\approx/g, '≈')
    .replace(/\\frac\{([^{}]+)\}\{([^{}]+)\}/g, '($1)/($2)')
    .replace(/\\sqrt\{([^{}]+)\}/g, '√($1)')
}

/** 去掉包裹数学片段的 `$...$` / `\(...\)`（内容含数字时才去，避免误伤普通文本）。 */
function unwrapMath(s: string): string {
  return s
    .replace(/\$\s*([^$\n]{1,120}?)\s*\$/g, (m, inner: string) =>
      /\d/.test(inner) ? inner : m
    )
    .replace(/\\\(\s*([^)\n]{1,120}?)\s*\\\)/g, (m, inner: string) =>
      /\d/.test(inner) ? inner : m
    )
}

/** 行内标记：行内代码 → 粗体 → 斜体 → 链接（行内代码先做，避免其内部被后续规则改写）。 */
function inline(raw: string): string {
  let out = raw
  out = out.replace(/`([^`\n]+)`/g, (_m, code: string) => `<code class="md-code">${code}</code>`)
  out = out.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
  out = out.replace(/__([^_\n]+)__/g, '<strong>$1</strong>')
  out = out.replace(/(^|[^*\w])\*([^*\n]+)\*/g, '$1<em>$2</em>')
  out = out.replace(/(^|[^_\w])_([^_\n]+)_/g, '$1<em>$2</em>')
  out = out.replace(/\[([^\]\n]+)\]\(([^)\s]+)\)/g, (_m, label: string, url: string) => {
    const safe = safeUrl(url)
    if (!safe) return label
    return `<a class="md-link" href="${safe}" target="_blank" rel="noopener noreferrer">${label}</a>`
  })
  return out
}

/**
 * 把模型输出渲染为安全 HTML 片段（供 v-html 使用）。
 *
 * 支持：代码围栏、标题、有序/无序列表、引用、行内代码、粗体、斜体、链接、段落；
 * 并做 LaTeX 记号简化与 `$...$` 去壳。流式期间内容不完整会渲染成"半截"HTML，
 * 随流式补全自动收敛，属预期行为。
 */
export function renderMarkdown(raw: string): string {
  const text = unwrapMath(simplifyLatex(escapeHtml(raw ?? '')))
  const lines = text.split('\n')
  const out: string[] = []

  let listType: 'ul' | 'ol' | null = null
  const closeList = () => {
    if (listType) {
      out.push(`</${listType}>`)
      listType = null
    }
  }

  let i = 0
  while (i < lines.length) {
    const line = lines[i]

    // 代码围栏 ``` / ~~~
    const fence = line.match(/^\s*(```|~~~)/)
    if (fence) {
      closeList()
      const marker = fence[1] === '```' ? '```' : '~~~'
      const closing = new RegExp(`^\\s*${marker === '```' ? '```' : '~~~'}`)
      const buf: string[] = []
      i++
      while (i < lines.length && !closing.test(lines[i])) {
        buf.push(lines[i])
        i++
      }
      i++ // 跳过收尾围栏
      out.push(`<pre class="md-pre"><code>${buf.join('\n')}</code></pre>`)
      continue
    }

    // 标题（渲染为 h4~h6，避免与页面既有 h1/h2 抢层级）
    const heading = line.match(/^(#{1,4})\s+(.*)$/)
    if (heading) {
      closeList()
      const level = Math.min(6, heading[1].length + 3)
      out.push(`<h${level} class="md-h">${inline(heading[2])}</h${level}>`)
      i++
      continue
    }

    // 分隔线：整行由 3 个及以上同一符号组成（*** / --- / ___，允许中间空格）
    if (/^\s*([-*_])\s*(?:\1\s*){2,}$/.test(line)) {
      closeList()
      out.push('<hr class="md-hr" />')
      i++
      continue
    }

    // 列表
    const ul = line.match(/^\s*[-*+]\s+(.*)$/)
    const ol = line.match(/^\s*\d+[.)]\s+(.*)$/)
    if (ul || ol) {
      const type: 'ul' | 'ol' = ul ? 'ul' : 'ol'
      if (listType !== type) {
        closeList()
        out.push(`<${type} class="md-list">`)
        listType = type
      }
      out.push(`<li>${inline((ul ?? ol)![1])}</li>`)
      i++
      continue
    }

    // 引用
    const quote = line.match(/^\s*>\s?(.*)$/)
    if (quote) {
      closeList()
      out.push(`<blockquote class="md-quote">${inline(quote[1])}</blockquote>`)
      i++
      continue
    }

    // 空行 → 分段
    if (line.trim() === '') {
      closeList()
      i++
      continue
    }

    closeList()
    out.push(`<p class="md-p">${inline(line)}</p>`)
    i++
  }

  closeList()
  return out.join('')
}
