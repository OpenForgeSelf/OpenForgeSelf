import { describe, expect, it } from 'vitest'
import { renderMarkdown } from './markdown'

/**
 * markdown 表格渲染（FR-207 契约外的真实高频缺口，041 修订）。
 *
 * 背景：7399 实测 LLM 输出 `| 城市 | 人口 |` 表格被渲染成纯文本 —— 契约最小集
 * （粗体/斜体/行内代码/代码块/列表/链接）没列表格，但真实场景天天用。
 * 断言绑定业务语义：管道表 + 分隔行 → <table>；且**先转义后白名单**的 XSS 防线不破。
 */

describe('renderMarkdown — 表格（GFM 管道表）', () => {
  it('标准三列表格：表头进 thead，数据行进 tbody', () => {
    const html = renderMarkdown('| 城市 | 人口（万） | GDP（亿元） |\n|------|-----------|------------|\n| 北京 | 2184 | 43760 |\n| 上海 | 2487 | 47218 |')
    expect(html).toContain('<table class="aiw-table">')
    expect(html).toContain('<thead>')
    expect(html.match(/<th>/g)).toHaveLength(3)
    // <tr> 共 3 个 = 1 表头 + 2 数据行
    expect(html.match(/<tr>/g)).toHaveLength(3)
    expect(html).toContain('<td>北京</td>')
    expect(html).toContain('<td>43760</td>')
    // 分隔行本身不产出 <tr>
    expect(html).not.toContain('---')
  })

  it('没有分隔行就不是表格（普通竖线文本原样输出，不误判）', () => {
    const html = renderMarkdown('a | b\n这是普通文本 | 带竖线')
    expect(html).not.toContain('<table')
    expect(html).toContain('a | b')
  })

  it('表格后面接列表/段落时正确闭合，不粘连', () => {
    const html = renderMarkdown('| A | B |\n|---|---|\n| 1 | 2 |\n\n普通段落\n- 列表项')
    expect(html).toContain('</tbody></table>')
    expect(html).toContain('普通段落')
    expect(html).toContain('<ul class="aiw-ul">')
    expect(html).toContain('<li>列表项</li>')
  })

  it('表格内单元格支持行内格式（粗体/行内代码）', () => {
    const html = renderMarkdown('| 项 | 值 |\n|---|---|\n| **重要** | `x=1` |')
    expect(html).toContain('<td><strong>重要</strong></td>')
    expect(html).toContain('<td><code class="aiw-inline-code">x=1</code></td>')
  })

  it('XSS：表格里的 HTML/脚本必须被转义（先转义后白名单的防线不破）', () => {
    const html = renderMarkdown('| 名 | 值 |\n|---|---|\n| <script>alert(1)</script> | <img src=x onerror=alert(1)> |')
    expect(html).not.toContain('<script>alert(1)</script>')
    expect(html).not.toContain('<img src=x')
    expect(html).toContain('&lt;script&gt;')
    expect(html).toContain('&lt;img')
  })

  it('列数少于表头时补空单元格，不错位', () => {
    const html = renderMarkdown('| A | B | C |\n|---|---|---|\n| 1 |')
    expect(html.match(/<td>/g)).toHaveLength(3)
  })

  it('表格在代码块之后仍能渲染（代码段不被表格逻辑吃掉）', () => {
    const html = renderMarkdown('```js\nconst a = 1\n```\n| A | B |\n|---|---|\n| 1 | 2 |')
    expect(html).toContain('<pre class="aiw-code"')
    expect(html).toContain('const a = 1')
    expect(html).toContain('<table class="aiw-table">')
  })
})
