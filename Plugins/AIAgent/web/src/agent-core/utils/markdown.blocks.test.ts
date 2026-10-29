import { describe, expect, it } from 'vitest'
import { renderMarkdown } from './markdown'

/**
 * 2026-10-26 现场截图暴露的缺陷回归：标题未解析、块间距失控、<br /> 插进表格。
 * 断言绑定业务语义，不绑定实现细节：
 *   - 标题必须产出块级元素并带 aria-level（不能用 h1~h6 标签，否则被宿主 reset 掉字号）
 *   - 空行不得产出任何高度（旧实现每个空行一个 <br />，这是「标题离表格很远」的根因）
 *   - 表格内外不得出现 <br />（旧实现全局替换 \n，把 <br /> 塞进 <tr> 之间）
 */
describe('renderMarkdown — 块级排版', () => {
  it('三级标题被解析成块级元素，不再是纯文本', () => {
    const html = renderMarkdown('### 二、根级文档与配置')
    expect(html).not.toContain('###')
    expect(html).toContain('aiw-h3')
    expect(html).toContain('aria-level="3"')
    expect(html).toContain('二、根级文档与配置')
  })

  it('一到六级标题各自映射到对应层级', () => {
    for (let n = 1; n <= 6; n += 1) {
      const html = renderMarkdown(`${'#'.repeat(n)} T${n}`)
      expect(html).toContain(`aiw-h${n}`)
      expect(html).toContain(`aria-level="${n}"`)
    }
  })

  it('空行不产出任何高度（块间距交给 CSS，不靠换行兼职）', () => {
    const html = renderMarkdown('第一段\n\n第二段')
    expect(html).not.toContain('<br />')
    expect(html.match(/<p class="aiw-p">/g)).toHaveLength(2)
  })

  it('标题与表格之间不产生多余换行（截图同款排版）', () => {
    const html = renderMarkdown('### 表\n\n| 工程 | 职责 |\n|---|---|\n| A | B |')
    expect(html).not.toContain('<br />')
    expect(html).toContain('aiw-table')
    // 表格必须包在滚动容器里，让窄屏可横滚而不是撑破气泡
    expect(html).toContain('aiw-table-wrap')
  })

  it('表格标记内部绝不出现 <br />（旧实现会把 br 塞进 tr 之间）', () => {
    const html = renderMarkdown('| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |')
    const start = html.indexOf('<table')
    const end = html.indexOf('</table>')
    expect(start).toBeGreaterThanOrEqual(0)
    expect(html.slice(start, end)).not.toContain('<br')
  })

  it('引用块被解析，且不残留 > 字符', () => {
    const html = renderMarkdown('> 测试采用同层配对\n> 无统一 tests 目录')
    expect(html).toContain('aiw-quote')
    expect(html).not.toContain('&gt;')
    expect(html).toContain('测试采用同层配对')
  })

  it('有序 / 无序列表各自成标签', () => {
    expect(renderMarkdown('1. 甲\n2. 乙')).toContain('aiw-ol')
    expect(renderMarkdown('- 甲\n- 乙')).toContain('aiw-ul')
    expect(renderMarkdown('1. 甲\n2. 乙')).not.toContain('aiw-ul')
  })

  it('分割线被解析（且不被误判成列表项）', () => {
    const html = renderMarkdown('上\n\n---\n\n下')
    expect(html).toContain('aiw-hr')
    expect(html).not.toContain('<li>')
  })

  it('段落内的软换行仍转 <br />（不能把多行段落压成一行）', () => {
    const html = renderMarkdown('第一行\n第二行')
    expect(html.match(/<br \/>/g)).toHaveLength(1)
    expect(html.match(/<p class="aiw-p">/g)).toHaveLength(1)
  })

  it('标题里的行内格式照常生效', () => {
    expect(renderMarkdown('## 看 **这里**')).toContain('<strong>这里</strong>')
  })

  it('XSS 防线不破：标题与引用同样先转义', () => {
    const html = renderMarkdown('### <img src=x onerror=alert(1)>\n\n> <script>alert(1)</script>')
    expect(html).not.toContain('<img src=x')
    expect(html).not.toContain('<script>')
    expect(html).toContain('&lt;img')
  })
})
